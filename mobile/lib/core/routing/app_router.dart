import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import '../auth/auth_state.dart';
import '../config/app_config.dart';
import '../layout/breakpoints.dart';
import '../network/failure.dart';
import '../network/session.dart';
import '../../features/auth/data/auth_api.dart';
import '../../features/auth/presentation/sign_in_screen.dart';
import '../../features/home/presentation/home_screen.dart';
import '../../features/home/presentation/providers.dart';
import '../../features/stokvel/presentation/stokvel_list_screen.dart';
import '../../features/stokvel/presentation/stokvel_screen.dart';
import '../../features/stokvel/presentation/cycle_screen.dart';
import '../../features/payouts/presentation/providers.dart';
import '../../features/payouts/presentation/payout_screen.dart';
import '../../features/shell/presentation/tabs.dart';
import '../../features/invites/presentation/invite_screen.dart';
import 'auth_redirect.dart';

class _AuthNotifier extends ChangeNotifier {
  void refresh() => notifyListeners();
}

final appRouterProvider = Provider.family<GoRouter, AppConfig>((ref, config) {
  final refresh = _AuthNotifier();
  ref.listen(fakeAuthProvider, (_, next) {
    // Switching identities clears every per-person read cache.
    ref.invalidate(memberHomeProvider);
    ref.invalidate(stokvelDetailsProvider);
    ref.invalidate(activityFeedProvider);
    ref.invalidate(payoutOrderProvider);
    if (next == null) ref.read(accessSessionProvider.notifier).clear();
    refresh.refresh();
  });
  ref.onDispose(refresh.dispose);
  final router = GoRouter(
    initialLocation: '/home',
    refreshListenable: refresh,
    redirect: (context, state) => authRedirect(
      location: state.uri.toString(),
      persona: ref.read(fakeAuthProvider),
      rememberedAddress: state.uri.queryParameters['from'],
    ),
    routes: [
      GoRoute(path: '/sign-in', builder: (_, _) => const SignInScreen()),
      StatefulShellRoute.indexedStack(
        builder: (context, state, shell) =>
            _AppShell(shell: shell, config: config),
        branches: [
          StatefulShellBranch(routes: [
            GoRoute(path: '/home', builder: (_, _) => const HomeScreen()),
            if (config.flavor == 'dev')
              GoRoute(path: '/dev/personas',
                builder: (_, _) => const _PersonaDevScreen()),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(path: '/stokvels',
              builder: (_, _) => const StokvelListScreen(), routes: [
                GoRoute(path: ':id',
                  builder: (_, state) => StokvelScreen(
                    initialId: state.pathParameters['id']!), routes: [
                    GoRoute(path: 'cycles/:cycleId',
                      builder: (_, state) => CycleScreen(
                        stokvelId: state.pathParameters['id']!,
                        cycleId: state.pathParameters['cycleId']!), routes: [
                        GoRoute(path: 'payouts', builder: (_, state) =>
                          PayoutScreen(
                            stokvelId: state.pathParameters['id']!,
                            cycleId: state.pathParameters['cycleId']!)),
                      ]),
                  ]),
              ]),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(path: '/activity',
              builder: (_, _) => const ActivityScreen()),
          ]),
          StatefulShellBranch(routes: [
            GoRoute(path: '/profile',
              builder: (_, _) => const ProfileScreen()),
          ]),
        ],
      ),
      GoRoute(path: '/join/:code', builder: (_, state) =>
          InviteScreen(code: state.pathParameters['code']!)),
    ],
    errorBuilder: (_, _) =>
        const Scaffold(body: Center(child: Text('Not found'))),
  );
  ref.onDispose(router.dispose);
  return router;
});

class _AppShell extends StatelessWidget {
  const _AppShell({required this.shell, required this.config});
  final StatefulNavigationShell shell;
  final AppConfig config;
  static const labels = ['Home', 'Stokvels', 'Activity', 'Profile'];
  static const icons = [Icons.home_outlined, Icons.groups_outlined,
    Icons.timeline_outlined, Icons.person_outline];
  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, constraints) {
      final wide = windowClass(constraints.maxWidth) != WindowClass.compact;
      Widget content = shell;
      if (wide) {
        content = Row(children: [
          NavigationRail(
            selectedIndex: shell.currentIndex,
            extended: windowClass(constraints.maxWidth) == WindowClass.expanded,
            onDestinationSelected: (i) => shell.goBranch(i,
              initialLocation: i == shell.currentIndex),
            labelType: windowClass(constraints.maxWidth) == WindowClass.expanded
              ? NavigationRailLabelType.none : NavigationRailLabelType.all,
            destinations: [for (var i = 0; i < 4; i++)
              NavigationRailDestination(icon: Icon(icons[i]),
                label: Text(labels[i]))]),
          const VerticalDivider(width: 1),
          Expanded(child: shell),
        ]);
      }
      return Scaffold(
        body: content,
        floatingActionButton: config.flavor == 'dev'
          ? FloatingActionButton.small(tooltip: 'Switch demo persona',
            onPressed: () => context.go('/dev/personas'),
            child: const Icon(Icons.switch_account))
          : null,
        bottomNavigationBar: wide ? null : NavigationBar(
          selectedIndex: shell.currentIndex,
          onDestinationSelected: (i) => shell.goBranch(i,
            initialLocation: i == shell.currentIndex),
          destinations: [for (var i = 0; i < 4; i++)
            NavigationDestination(icon: Icon(icons[i]), label: labels[i])]),
      );
    });
}

String _seedEmail(String person) => switch (person) {
  'lerato' => const String.fromEnvironment('leratoEmail'),
  'sipho' => const String.fromEnvironment('siphoEmail'),
  'nomsa' => const String.fromEnvironment('nomsaEmail'),
  _ => '',
};

class _PersonaDevScreen extends ConsumerStatefulWidget {
  const _PersonaDevScreen();
  @override
  ConsumerState<_PersonaDevScreen> createState() => _PersonaDevScreenState();
}

class _PersonaDevScreenState extends ConsumerState<_PersonaDevScreen> {
  bool busy = false;
  Failure? error;

  Future<void> switchTo(String person) async {
    if (busy) return;
    setState(() { busy = true; error = null; });
    final password = const String.fromEnvironment('seedPassword');
    final email = _seedEmail(person);
    if (password.isEmpty || email.isEmpty) {
      setState(() {
        error = const RuleFailure('Provide seed emails and password with --dart-define.');
        busy = false;
      });
      return;
    }
    try {
      // Clear the last user before any network request, so stale balances
      // cannot be displayed as the newly authenticated identity.
      ref.read(accessSessionProvider.notifier).clear();
      final session = await ref.read(authApiProvider).login(email, password);
      ref.read(accessSessionProvider.notifier).start(session.token, session.expiresAt);
      ref.read(fakeAuthProvider.notifier).signIn(person);
      if (mounted) context.go('/home');
    } catch (e) {
      if (mounted) setState(() =>
        error = e is Failure ? e : const UnexpectedFailure());
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Dev persona switcher')),
    body: ListView(children: [
      if (error case final failure?) ListTile(
        title: Text(failure.message),
        subtitle: failure.reference == null
          ? null : SelectableText('Reference: ${failure.reference}')),
      for (final person in const ['lerato', 'sipho', 'nomsa'])
        ListTile(title: Text(person),
          enabled: !busy, onTap: () => switchTo(person)),
    ]),
  );
}
