import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/network/failure.dart';
import '../data/auth_api.dart';

class SignInScreen extends ConsumerStatefulWidget {
  const SignInScreen({super.key});

  @override
  ConsumerState<SignInScreen> createState() => _SignInScreenState();
}

class _SignInScreenState extends ConsumerState<SignInScreen> {
  final _email = TextEditingController();
  final _password = TextEditingController();
  bool _busy = false;
  Failure? _error;

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_busy) return;
    setState(() { _busy = true; _error = null; });
    try {
      await signInWithApi(ref, _email.text.trim(), _password.text,
        persona: 'authenticated');
    } on Object catch (error) {
      if (mounted) setState(() => _error = error is Failure
          ? error : const UnexpectedFailure());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Sign in to RondiTrack')),
    body: Center(child: ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 420),
      child: ListView(shrinkWrap: true,
        padding: const EdgeInsets.all(16),
        children: [
          const Text('Sign in with your RondiTrack API account.'),
          const SizedBox(height: 12),
          TextField(key: const Key('login-email'), controller: _email,
            enabled: !_busy, keyboardType: TextInputType.emailAddress,
            autofillHints: const [AutofillHints.email],
            decoration: const InputDecoration(labelText: 'Email')),
          TextField(key: const Key('login-password'), controller: _password,
            enabled: !_busy, obscureText: true,
            autofillHints: const [AutofillHints.password],
            onSubmitted: (_) => _submit(),
            decoration: const InputDecoration(labelText: 'Password')),
          if (_error case final failure?) ...[
            const SizedBox(height: 12),
            Text(failure.message),
            if (failure.reference case final reference?)
              SelectableText('Reference: $reference'),
          ],
          const SizedBox(height: 12),
          FilledButton(key: const Key('login-submit'),
            onPressed: _busy ? null : _submit,
            child: _busy ? const Text('Signing in...') : const Text('Sign in')),
        ],
      ),
    )),
  );
}
