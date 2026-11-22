import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ronditrack_mobile/features/home/data/api_home_repository.dart';
import 'package:ronditrack_mobile/features/payouts/data/api_payout_repository.dart';
import 'package:ronditrack_mobile/features/payouts/domain/payout_models.dart';
import 'package:ronditrack_mobile/core/network/failure.dart';

final homeJson = {
  'stokvels': [
    {
      'stokvelId': '11111111-1111-4111-8111-111111111111',
      'name': 'Mabena Family',
      'role': 'Treasurer',
      'arrearsCents': 1250,
      'nextPayoutDueUtc': '2026-11-30T12:00:00Z',
      'currentCycle': {
        'id': '22222222-2222-4222-8222-222222222222',
        'label': '2026-11',
        'status': 'Open',
        'paidCents': 15050,
        'owedCents': 49950,
        'endsOn': '2026-11-30',
      },
    },
  ],
};

Dio stubDio(Map<String, Object> fixtures) {
  final dio = Dio(BaseOptions(baseUrl: 'https://example.invalid'));
  dio.interceptors.add(InterceptorsWrapper(
    onRequest: (options, handler) {
      final response = fixtures[options.path];
      if (response == null) {
        handler.reject(DioException(requestOptions: options,
          response: Response<dynamic>(requestOptions: options,
            statusCode: 404, data: {'detail': 'Not found'})));
        return;
      }
      handler.resolve(Response<dynamic>(
        requestOptions: options, statusCode: 200, data: response));
    },
  ));
  return dio;
}

void main() {
  test('Home maps the actual API fields to statuses, cents and SA date', () async {
    final repository = ApiHomeRepository(stubDio({'/api/v1/me/home': homeJson}));
    final home = await repository.loadHome('ignored-by-api');
    expect(home, hasLength(1));
    expect(home.single.membership.role.name, 'treasurer');
    expect(home.single.arrearsCents.value, 1250);
    expect(home.single.currentCycle!.paidCents.value, 15050);
    expect(home.single.currentCycle!.endsOnDate, '2026-11-30');
    expect(home.single.nextPayoutDueUtc!.isUtc, isTrue);
  });

  test('unknown cycle status remains unknown, not a crash', () {
    final row = Map<String, dynamic>.from((homeJson['stokvels']! as List).single as Map);
    row['currentCycle'] = {
      ...Map<String, dynamic>.from(row['currentCycle'] as Map),
      'status': 'NewFutureStatus',
    };
    expect(mapHomeEntry(row).currentCycle!.status.name, 'unknown');
  });

  test('non-member detail is Not found without reading contributions', () async {
    final repository = ApiHomeRepository(stubDio({'/api/v1/me/home': {'stokvels': <Map<String, dynamic>>[]}}));
    await expectLater(repository.loadDetail('persona', 'inaccessible'),
      throwsA(isA<NotFoundFailure>()));
  });

  test('detail translates API decimal rands into integer cents', () async {
    const id = '11111111-1111-4111-8111-111111111111';
    final repository = ApiHomeRepository(stubDio({
      '/api/v1/me/home': homeJson,
      '/api/v1/stokvels/$id/contributions': {
        'items': [
          {'id':'33333333-3333-4333-8333-333333333333',
           'userId':'44444444-4444-4444-8444-444444444444',
           'stokvelId': id,
           'contributionCycleId': '22222222-2222-4222-8222-222222222222',
           'amount':150.50, 'recordedAt':'2026-11-21T10:30:00Z'},
        ],
        'nextPageToken': null,
      },
    }));
    final details = await repository.loadDetail('persona', id);
    expect(details.contributions.single.amountCents.value, 15050);
    expect(details.contributions.single.recordedAtUtc.isUtc, isTrue);
  });

  test('payout order and next member come straight from the API', () async {
    const id = '11111111-1111-4111-8111-111111111111';
    const cycle = '22222222-2222-4222-8222-222222222222';
    final repository = ApiPayoutRepository(stubDio({
      '/api/v1/stokvels/$id/cycles/$cycle/payout/order': {
        'stokvelId': id, 'cycleId': cycle, 'nextMemberId': 'next-person',
        'entries': [
          {'memberId':'next-person','memberName':'Someone',
           'position':3,'amountCents':20000,'status':'Next','paidOnUtc':null},
          {'memberId':'first-person','memberName':'Another',
           'position':1,'amountCents':20000,'status':'Future','paidOnUtc':null},
        ],
      },
    }));
    final order = await repository.loadOrder('ignored-by-api', id, cycle);
    expect(order.entries.map((e) => e.position), [3, 1]);
    expect(order.nextMemberId, 'next-person');
    expect(order.entries[0].status, PayoutStatus.next);
    expect(order.entries[1].status, PayoutStatus.unknown);
  });
}
