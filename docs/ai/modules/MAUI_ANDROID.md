# MAUI / Android project facts

Populate only for an applicable MAUI Android project. Keep build/deploy/device
procedures in the `maui-android` skill; activation belongs in PROJECT_BRIEF.

## Projects and Android target

| Fact | Project-specific value |
| --- | --- |
| MAUI projects | [TODO: actual project paths] |
| Android target framework | [TODO: actual TFM] |
| API / minimum OS / architecture | [TODO: project values and supported ABIs] |
| Package/application IDs | [TODO: identifiers by configuration] |
| Application entry points | [TODO: startup, activity and initial navigation] |

## Canonical build / run targets

- **Build:** [TODO: project, TFM, configuration and required options.]
- **Run/deploy:** [TODO: supported target, artifact and deployment method.]
- **Tests:** [TODO: shared logic tests and required device scenarios.]

## Device assumptions

[TODO: selection criteria, supported runtime/API/ABI, authorization and connection
requirements. Discover current devices before device-dependent work.]

## Deployment-specific information

- **Artifact/package/activity:** [TODO: actual build/manifest values.]
- **Services/ports:** [TODO: backend and forwarding requirements.]
- **Test data:** [TODO: preservation/reset implications.]
- **Development signing:** [TODO: setup references; no secrets.]

## Optional tooling configuration

- **DevFlow:** [TODO: intentional opt-in, connection and app instrumentation.]
- **Emulator:** [TODO: configured AVD/system image, if enabled.]
- **Source debugger compatibility:** [TODO: actual runtime/transport evidence;
  desktop CoreCLR acceptance does not establish Android compatibility.]
