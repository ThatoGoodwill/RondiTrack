import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/auth/auth_state.dart';

class SignInScreen extends ConsumerWidget {
  const SignInScreen({super.key});
  @override
  Widget build(BuildContext context, WidgetRef ref) => Scaffold(
    appBar: AppBar(title: const Text('RondiTrack sign in')),
    body: Center(child: ConstrainedBox(constraints: const BoxConstraints(maxWidth: 420),
      child: ListView(shrinkWrap: true, padding: const EdgeInsets.all(16), children: [
        const Text('Week 10 demo sign-in — no password or live API.'),
        const SizedBox(height: 12),
        for (final person in const [('lerato', 'Lerato'), ('sipho', 'Sipho'), ('nomsa', 'Nomsa')])
          Padding(padding: const EdgeInsets.symmetric(vertical: 4), child: FilledButton(
            key: Key('sign-in-${person.$1}'), onPressed: () => ref.read(fakeAuthProvider.notifier).signIn(person.$1),
            child: Text('Continue as ${person.$2}'))),
      ]))),
  );
}
