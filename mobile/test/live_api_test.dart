import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ronditrack_mobile/features/auth/data/auth_api.dart';
import 'package:ronditrack_mobile/features/home/data/api_home_repository.dart';
import 'package:ronditrack_mobile/features/home/domain/models.dart';
import 'package:ronditrack_mobile/features/stokvel/domain/statuses.dart';

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
    Future<(Dio, List<HomeEntry>)> loginAndHome(String email) async {
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
    final leratoIds = leratoHome.map((e) => e.membership.stokvelId).toSet();
    final siphoIds = siphoHome.map((e) => e.membership.stokvelId).toSet();
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
    // A Member cannot issue a Treasurer-only payout. Authorization
    // rejects this before a money operation can begin.
    final members = siphoHome.where((entry) =>
      entry.membership.role == StokvelRole.member &&
      entry.currentCycle != null).toList();
    expect(members, isNotEmpty);
    final member = members.first;
    final forbidden = await siphoDio.post<dynamic>(
      '/api/v1/stokvels/${member.membership.stokvelId}'
      '/cycles/${member.currentCycle!.id}/payout',
      options: Options(validateStatus: (_) => true));
    expect(forbidden.statusCode, 403);
    expect(leratoHome, isNotEmpty);
  });
}
