import 'package:flutter_riverpod/flutter_riverpod.dart';

/// In-memory access token only. Never write it to logs or local preferences.
class AccessSession {
  const AccessSession(this.token, this.expiresAt);
  final String token;
  final DateTime expiresAt;
}

class AccessSessionController extends Notifier<AccessSession?> {
  @override
  AccessSession? build() => null;

  void start(String token, DateTime expiresAt) =>
      state = AccessSession(token, expiresAt.toUtc());

  void clear() => state = null;
}

final accessSessionProvider =
    NotifierProvider<AccessSessionController, AccessSession?>(
  AccessSessionController.new,
);
