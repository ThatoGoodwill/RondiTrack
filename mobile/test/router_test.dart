import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ronditrack_mobile/app/ronditrack_app.dart';
import 'package:ronditrack_mobile/core/auth/auth_state.dart';
import 'package:ronditrack_mobile/core/config/app_config.dart';
import 'package:ronditrack_mobile/core/routing/app_router.dart';
import 'package:ronditrack_mobile/features/home/data/fake_home_repository.dart';
import 'package:ronditrack_mobile/features/home/presentation/providers.dart';

class SignedInPersona extends FakeAuth {
  @override
  String? build() => 'lerato';
}

void main() {
  final prod = AppConfig(flavor: 'prod', apiBaseUrl: Uri.parse('https://example.org'));

  testWidgets('signed-out deep link survives fake sign in', (tester) async {
    final container = ProviderContainer(overrides: [
      homeRepositoryProvider.overrideWithValue(const FakeHomeRepository(delay: Duration.zero)),
    ]);
    addTearDown(container.dispose);
    final router = container.read(appRouterProvider(prod));
    // Reuse the previously created router instance.
    router.go('/join/HELLO');
    await tester.pumpWidget(UncontrolledProviderScope(
      container: container, child: RondiTrackApp(config: prod)));
    await tester.pumpAndSettle();
    expect(find.text('RondiTrack sign in'), findsOneWidget);
    await tester.tap(find.byKey(const Key('sign-in-lerato')));
    await tester.pumpAndSettle();
    expect(find.text('Invite code: HELLO'), findsOneWidget);
  });

  testWidgets('non-member sees Not found, never permission details', (tester) async {
    final container = ProviderContainer(overrides: [
      fakeAuthProvider.overrideWith(SignedInPersona.new),
      homeRepositoryProvider.overrideWithValue(const FakeHomeRepository(delay: Duration.zero)),
    ]);
    addTearDown(container.dispose);
    final router = container.read(appRouterProvider(prod));
    // Reuse the previously created router instance.
    router.go('/stokvels/unknown');
    await tester.pumpWidget(UncontrolledProviderScope(
      container: container, child: RondiTrackApp(config: prod)));
    await tester.pumpAndSettle();
    expect(find.text('Not found'), findsOneWidget);
  });

  testWidgets('prod rejects the dev persona switcher route', (tester) async {
    final container = ProviderContainer(overrides: [
      fakeAuthProvider.overrideWith(SignedInPersona.new),
    ]);
    addTearDown(container.dispose);
    final router = container.read(appRouterProvider(prod));
    // Reuse the previously created router instance.
    router.go('/dev/personas');
    await tester.pumpWidget(UncontrolledProviderScope(
      container: container, child: RondiTrackApp(config: prod)));
    await tester.pumpAndSettle();
    expect(find.text('Dev persona switcher'), findsNothing);
    expect(find.text('Not found'), findsOneWidget);
    expect(find.byTooltip('Switch demo persona'), findsNothing);
  });
}
