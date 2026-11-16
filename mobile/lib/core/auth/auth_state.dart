import 'package:flutter_riverpod/flutter_riverpod.dart';

/// A fake login for the Week 10 shell; production authentication is Week 11.
class FakeAuth extends Notifier<String?> {
  @override
  String? build() => null;

  void signIn(String persona) {
    if (!const {'lerato', 'sipho', 'nomsa'}.contains(persona)) {
      throw ArgumentError.value(persona, 'persona');
    }
    state = persona;
  }

  void signOut() => state = null;
}

final fakeAuthProvider = NotifierProvider<FakeAuth, String?>(FakeAuth.new);
