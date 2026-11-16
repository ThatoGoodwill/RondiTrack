import 'package:flutter/material.dart';

/// A fake invite preview. The actual join command is not performed by Week 10 fakes.
class InviteScreen extends StatelessWidget {
  const InviteScreen({required this.code, super.key});
  final String code;
  @override Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Invitation preview')),
    body: Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
      const Text('Stokvel invitation'),
      Text('Invite code: $code'),
      const Text('Joining will be available when the API is connected.'),
    ])));
}
