import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Kept as the navigation identity; Week 11 obtains a real API access token.
class FakeAuth extends Notifier<String?> {
  @override
  String? build() => null;

  void signIn(String persona) {
    if (!const {'lerato', 'sipho', 'nomsa', 'authenticated'}.contains(persona)) {
      throw ArgumentError.value(persona, 'persona');
    }
    state = persona;
  }

  void signOut() => state = null;
}

final fakeAuthProvider = NotifierProvider<FakeAuth, String?>(FakeAuth.new);
