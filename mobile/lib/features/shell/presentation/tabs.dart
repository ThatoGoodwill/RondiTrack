import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/auth/auth_state.dart';
import '../../home/presentation/providers.dart';

class ActivityScreen extends ConsumerWidget {
  const ActivityScreen({super.key});
  @override Widget build(BuildContext context, WidgetRef ref) {
    final activity = ref.watch(activityFeedProvider);
    return Scaffold(appBar: AppBar(title: const Text('Activity')),
      body: activity.when(loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: TextButton(onPressed: () => ref.invalidate(activityFeedProvider),
          child: const Text('Could not load activity. Try again'))),
        data: (items) => items.isEmpty
          ? const Center(child: Text('No activity yet. Join a stokvel to get started.'))
          : ListView(children: [for (final item in items) ListTile(title: Text(item))])));
  }
}

class ProfileScreen extends ConsumerWidget {
  const ProfileScreen({super.key});
  @override Widget build(BuildContext context, WidgetRef ref) {
    final persona = ref.watch(fakeAuthProvider);
    return Scaffold(appBar: AppBar(title: const Text('Profile')),
      body: ListView(children: [
        ListTile(title: const Text('Signed in as'), subtitle: Text(persona ?? 'Signed out')),
        ListTile(title: const Text('Sign out'), leading: const Icon(Icons.logout),
          onTap: () => ref.read(fakeAuthProvider.notifier).signOut()),
      ]));
  }
}
