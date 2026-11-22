import 'package:dio/dio.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/network/client.dart';
import '../../../core/network/failure_mapper.dart';
import '../../../core/network/session.dart';
import '../../../core/auth/auth_state.dart';

/// Contract: POST /api/v1/auth/login => accessToken and expiresAt.
class AuthApi {
  const AuthApi(this.dio);
  final Dio dio;

  Future<AccessSession> login(String email, String password) async {
    try {
      final response = await dio.post<dynamic>('/api/v1/auth/login',
        data: {'email': email, 'password': password});
      final data = Map<String, dynamic>.from(response.data as Map);
      return AccessSession(
        data['accessToken'] as String,
        DateTime.parse(data['expiresAt'] as String).toUtc());
    } catch (error) {
      throw mapFailure(error);
    }
  }
}

final authApiProvider = Provider<AuthApi>(
  (ref) => AuthApi(ref.watch(dioProvider)),
);

/// Only keep the access token in memory; cookie refresh is Week 11 Day 2.
Future<void> signInWithApi(WidgetRef ref, String email, String password,
    {required String persona}) async {
  final session = await ref.read(authApiProvider).login(email, password);
  ref.read(accessSessionProvider.notifier).start(session.token, session.expiresAt);
  ref.read(fakeAuthProvider.notifier).signIn(persona);
}
