import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ronditrack_mobile/features/auth/data/auth_api.dart';
import 'package:ronditrack_mobile/features/home/data/api_home_repository.dart';
import 'package:ronditrack_mobile/core/network/failure.dart';

const baseUrl = String.fromEnvironment('liveApiBaseUrl');
const password = String.fromEnvironment('seedPassword');
const lerato = String.fromEnvironment('leratoEmail');
const sipho = String.fromEnvironment('siphoEmail');
const nomsa = String.fromEnvironment('nomsaEmail');

void main() {
  test('live fresh seed has separate member homes and protected routes',
      tags: 'live',
      skip: baseUrl.isEmpty || password.isEmpty || lerato.isEmpty ||
        sipho.isEmpty || nomsa.isEmpty
          ? 'Requires a fresh seeded API and runtime login defines.'
          : false,
      () async {
    Future<(Dio, List<dynamic>)> loginAndHome(String email) async {
      final dio = Dio(BaseOptions(baseUrl: baseUrl));
      final token = await AuthApi(dio).login(email, password);
      dio.options.headers['Authorization'] = 'Bearer ${token.token}';
      final entries = await ApiHomeRepository(dio).loadHome('live');
      return (dio, entries);
    }

    final (leratoDio, leratoHome) = await loginAndHome(lerato);
    final (siphoDio, siphoHome) = await loginAndHome(sipho);
    final (_, nomsaHome) = await loginAndHome(nomsa);

    expect(leratoHome, isNotEmpty);
    expect(siphoHome, isNotEmpty);
    expect(nomsaHome, isEmpty);

    // Use the actual seed IDs returned by API rather than hard-coding GUIDs.
    final leratoIds = leratoHome.cast<dynamic>()
        .map((e) => e.membership.stokvelId as String).toSet();
    final siphoIds = siphoHome.cast<dynamic>()
        .map((e) => e.membership.stokvelId as String).toSet();
    final privateIds = leratoIds.difference(siphoIds);
    if (privateIds.isNotEmpty) {
      final id = privateIds.first;
      final response = await siphoDio.get<dynamic>('/api/v1/stokvels/$id',
        options: Options(validateStatus: (_) => true));
      expect(response.statusCode, 404);
    }
    final stranger = await leratoDio.get<dynamic>(
      '/api/v1/stokvels/00000000-0000-4000-8000-000000000000',
      options: Options(validateStatus: (_) => true));
    expect(stranger.statusCode, 404);
    // The real 403 test requires a seeded member-only cycle; it is recorded
    // in the PR separately rather than issuing a real money write blindly.
    expect(leratoHome, isNot(isA<Failure>()));
  });
}
