# RondiTrack Mobile — Assignment 10.1 (DRAFT)

**This is the Week 10 Flutter app source, staged under `mobile/` in the API repository because a separate app repository was not available in the connected GitHub account.** The assignment requires moving the contents of this folder to the ROOT of a separate `RondiTrack-Mobile` repository. It is not a deployment of Assignment 9.3.

## Contract notes

API repository: `ThatoGoodwill/RondiTrack`, commit `6f56cfd4c404aae15643d34a8ea9421ec341e0ab`, copied to `contract/openapi-v1.json`.

| Question | API response / precise spelling |
| --- | --- |
| Cycle statuses | `Open`, `PaidOut`, `Closed` |
| Stokvel membership roles | `Member`, `Treasurer`, `Admin`; read from `/api/v1/me/home` per stokvel, never the user/token |
| Contribution status | The existing `ContributionResponse` has no `status` field. Mobile's `notRecorded/recorded` is temporary presentation state, not an invented API field |
| Home data | `stokvelId`, `name`, `role`, nullable `currentCycle` (id, label, paidCents, owedCents, status, nullable `endsOn`), `arrearsCents`, nullable `nextPayoutDueUtc` |
| Due dates | `endsOn` is a date-only `YYYY-MM-DD` string, not a UTC instant; `nextPayoutDueUtc` is a UTC timestamp |
| Seed personas | Lerato: Member of Ubuntu Savers, Treasurer of Umoja Grocery Club. Sipho: Treasurer/Admin across Ubuntu/Friday Rotation. Nomsa: no stokvel, empty state. |

Week 11 must regenerate the API contract from a live API (not silently guess missing fields).

## Running

Needs Flutter **3.47.7**, Dart **3.13+**, Android SDK/licences, and for iOS a Mac/Xcode. Run `flutter doctor` and accept licences before running.

1. Move `mobile/` to the root of the dedicated app repo.
2. `flutter create . --org za.co.thatogoodwill --project-name ronditrack_mobile --platforms android,ios`.
3. `python tool/setup_android_flavors.py` sets Android product flavors, application IDs, and launch names (no AGP9-incompatible `resValue`).
4. `flutter pub get`; then `dart run build_runner build`. Generated Freezed and Riverpod code is **not** committed; CI generates it too.
5. Run `flutter run --flavor dev --dart-define-from-file=config/dev.json`, or build `staging` and `prod` similarly. Only the `apiBaseUrl` changes by flavor; no secrets are stored in config files.
6. `dart format --output=none --set-exit-if-changed lib test`; `dart analyze --fatal-infos` (includes Riverpod plugin); `flutter test`.

**Outstanding native setup:** Three Android flavors are supplied through `tool/setup_android_flavors.py`. iOS scheme and build configurations still must be created in Xcode and checked on a Mac before this is a completed hand-in. At present the CI uses an in-job scaffold; commit the native project once generated. The Flutter lockfile also needs to be committed.

## Money

All amounts are a Dart 3 `Cents` extension type over `int`. Formatting never calculates decimal rands as doubles; spaces are U+00A0. Typed rands may use a comma or a full stop as a decimal mark, but neither can be a thousands delimiter. A parser refuses more than two decimals, ambiguous `1,500.50`, negatives and amounts above R1,000,000.00.

## Feature and state ownership

| Provider | Lifetime | Reason |
| --- | --- | --- |
| `homeRepositoryProvider` | keepAlive | Repository instance has no user-specific data; re-used per session |
| `selectedPersonaProvider` | keepAlive | Current fake persona until sign-out or manual switch |
| `memberHomeProvider` | autoDispose | Home list can be reloaded on return; no stale personal balance |
| `stokvelDetailsProvider(id)` | autoDispose family | One cache key per stokvel; changing id never shows the previous stokvel's data |

The fake repository lives entirely under `data/`. Presentations import only `presentation/providers.dart` and domain models; changing to HTTP in Week 11 changes the `homeRepositoryProvider` binding, not the widgets. When real authentication is added, sign-out must tear down the session's ProviderScope so keepAlive providers cannot show the previous person's data on a shared device.

## Four UI states

Loading is a spinner, empty explicitly says there are no stokvels, data renders cards, and failure has a message and Retry. Switching the detail selector changes the family provider argument, so its new loading view cannot show the previous member's contributions.

## Tests / CI

`test/money_test.dart`, `domain_test.dart`, `config_test.dart`, `view_state_test.dart`, `home_widget_test.dart`, `detail_provider_test.dart` cover core behavior. `.github/workflows/mobile-ci.yml` in the API repository currently exercises the staged app; move it into the app repo's `.github/workflows/` after moving the app.

This draft still needs: all Flutter CI green; tests for provider disposal, single-fetch and slow switch frame; all four home/detail UI states; an intentionally failing Riverpod lint PR followed by fixed CI; Android/iOS flavour installation evidence and screenshot/recording. Do **not** claim these checks have passed until they are verified.
