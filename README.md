# dotnet-testing-demo

A minimal .NET order-pricing API, built specifically to demonstrate the differences between **unit**, **integration**, and **functional/end-to-end (E2E)** testing — and how **regression testing** fits in as a practice rather than a fourth technique.

The app itself is intentionally small: an `OrderCalculator` that validates line items, sums a subtotal, and applies a discount via an injected `IDiscountService`. Small enough to hold in your head, with just enough real logic (validation rules, a dependency worth mocking, a discount threshold) to make each testing layer meaningful rather than trivial.

---

## The three layers, and why each one exists

| Layer | Project | What it actually exercises | Speed |
|---|---|---|---|
| Unit | `OrderApi.UnitTests` | `OrderCalculator` alone, with `IDiscountService` mocked via Moq. No HTTP, no DI container, no ASP.NET Core at all. | Milliseconds |
| Integration | `OrderApi.IntegrationTests` | The real ASP.NET Core pipeline — routing, model binding, the DI container, the *real* `DiscountService` — via `WebApplicationFactory`. Runs in-memory: no real OS process, no real TCP socket. | Fast (still in-process) |
| Functional/E2E | `OrderApi.FunctionalTests` | The actual **compiled, published** app, spawned as its own OS process on a real port, hit purely over HTTP. This test project has no `ProjectReference` to the app at all — it only knows the JSON wire contract, exactly like a real client would. | Slowest (real process startup, real network) |

The boundary between integration and functional/E2E is the part people most often blur together, so it's worth being explicit: **integration tests still take a shortcut** — `WebApplicationFactory` boots your app's real pipeline, but inside the test process, with an in-memory `TestServer` standing in for a real socket. **Functional/E2E tests take no shortcut** — they `dotnet publish` the app and launch it exactly as it would run in production, then talk to it the only way an external client can: real HTTP over a real port.

## What about regression testing?

Regression testing isn't a fourth layer with its own tools — it's the **practice** of running the tests you already have (all three layers, or a curated subset) automatically on every change, specifically to catch previously-working behavior breaking again. That's exactly what `.github/workflows/test.yml` does: unit, integration, and functional tests all run on every push and PR. The CI pipeline *is* the regression test suite. If someone changes the discount threshold and forgets to update the rate calculation, the unit tests catch it in milliseconds; if someone breaks the route wiring, integration tests catch it; if someone breaks something only visible when the app actually starts as a real process (a missing config value, a port binding issue), the functional tests catch it. Running all three together, every time, is what makes the combination a regression suite rather than three disconnected test projects.

---

## Running it on OpenShift (Red Hat Developer Sandbox)

This is the primary way this repo is meant to be run — no local .NET SDK required, since the Sandbox itself builds and tests everything.

### 1. Target your project

```bash
oc project <your-namespace>
```

### 2. Build and deploy the app (unit + integration tests gate this build)

```bash
oc apply -f openshift/01-build.yaml
oc start-build order-api --follow
```

Watch that build log closely — this is where `dotnet test` runs for the unit and integration layers, inside the image build itself. **If either test project fails, the build fails and no image is produced.** That's the same fail-the-build-on-real-findings pattern as `npm-scan-demo`'s `npm audit` gate, just enforced one layer down, at the container build rather than CI.

Once the build succeeds:

```bash
oc apply -f openshift/02-deploy.yaml
oc get pods -l app=order-api -w
```

Wait for the pod to show `1/1 Running`, then confirm it's actually healthy:

```bash
export ROUTE=$(oc get route order-api -o jsonpath='{.spec.host}')
curl -sk https://$ROUTE/health
```

### 3. Build the functional test runner image

```bash
oc apply -f openshift/03-functional-test-build.yaml
oc start-build order-api-functional-tests --follow
```

This image contains *only* the functional test project — no reference to the app's source at all, matching the black-box design of that layer.

### 4. Run the functional/E2E tests as a Job against the real deployed pod

```bash
oc create job functional-tests-$(date +%s) \
  --image=order-api-functional-tests:latest \
  --env=ORDERAPI_BASE_URL=http://order-api:8080 \
  -- dotnet test -c Release --no-build --logger "console;verbosity=normal"
```

`order-api:8080` here is the in-cluster Service DNS name — the Job runs inside the same namespace, so it reaches the real deployed pod over the real cluster network, not localhost. Each run needs a fresh Job name (hence the timestamp), since Job pod specs are immutable once created. Watch it:

```bash
oc get pods -l job-name --sort-by=.metadata.creationTimestamp
oc logs -f job/<the-job-name-just-created>
```

Clean up old test-run Jobs once you're done with them:

```bash
oc delete job -l app=order-api
```

### What this demonstrates end-to-end

- **Unit + integration tests** gate whether an image can be built at all — a broken image never reaches the cluster.
- **Functional/E2E tests** validate the actual thing that got deployed, over the real network, from a genuinely separate pod — the strongest guarantee this repo can give that "it works" isn't just "it works in a test harness."
- All of this on infrastructure that mirrors what you'd build in a real Tekton pipeline or Jenkins job: a test-gated image build, followed by a deploy, followed by a post-deploy validation stage.

---

## Coming later: DevSpaces

This project would also work well as a Red Hat OpenShift Dev Spaces workspace (browser-based, containerized dev environment) — parked for now, but worth returning to once the core testing/deploy flow above is solid.

---

## Running it locally (if you have the .NET SDK)

**Unit tests:**
```bash
dotnet test tests/OrderApi.UnitTests
```

**Integration tests:**
```bash
dotnet test tests/OrderApi.IntegrationTests
```

**Functional/E2E tests** — these need a published build first, since they spawn it as a real process:
```bash
dotnet publish src/OrderApi -c Release -o ./publish
ORDERAPI_DLL_PATH="$(pwd)/publish/OrderApi.dll" dotnet test tests/OrderApi.FunctionalTests
```

**Everything at once** (unit + integration only — functional needs the publish step above first):
```bash
dotnet test
```

**Run the app directly**, if you want to poke at it with `curl`:
```bash
dotnet run --project src/OrderApi
curl -X POST http://localhost:5000/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId":"me","items":[{"sku":"WIDGET","quantity":4,"unitPrice":30.00}]}'
```

---

## Mapping this to an enterprise pipeline (ADO)

This repo uses GitHub Actions since it's a public personal repo, but the same three-job shape maps directly onto an Azure DevOps pipeline: three stages (or parallel jobs) each running `dotnet test` against a different project, each publishing its `.trx` results as a pipeline artifact via the `PublishTestResults` task, with the functional stage adding a `dotnet publish` step before it runs. Nothing about the testing strategy itself is GitHub-specific — only the YAML dialect changes.

---

## A note on how this was built

This repo's C# code, Dockerfiles, and OpenShift manifests were written and reviewed carefully, but not compiled or run before being handed off — there's no .NET SDK, Docker, or OpenShift cluster access available in the environment that built this. The `oc start-build` logs are the real compile/test check. If a build fails, that's expected to be entirely possible on a first pass — paste back what the log actually says and it's straightforward to fix from there, the same way earlier issues (a bad action version pin, a deprecated action major) got resolved on `npm-scan-demo`.
