import 'package:flutter_test/flutter_test.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:ronditrack_mobile/features/home/data/fake_home_repository.dart';
import 'package:ronditrack_mobile/features/home/domain/not_found.dart';
import 'package:ronditrack_mobile/features/payouts/data/fake_payout_repository.dart';
import 'package:ronditrack_mobile/features/payouts/domain/payout_models.dart';
import 'package:ronditrack_mobile/features/payouts/presentation/providers.dart';
import 'package:ronditrack_mobile/features/home/presentation/view_state.dart';

void main() {
  final repo = FakePayoutRepository(const FakeHomeRepository(delay: Duration.zero));
  test('unknown API status is safe', () {
    expect(PayoutStatus.fromApi('Unexpected'), PayoutStatus.unknown);
    for (final status in PayoutStatus.values) {
      expect(status.label.isNotEmpty, isTrue);
    }
  });
  test('non-member gets not found', () async {
    await expectLater(repo.loadOrder('nomsa', 'mabena', 'mabena-cycle'), throwsA(isA<StokvelNotFound>()));
  });
  test('fixture maintains server order, including next', () async {
    final value = await repo.loadOrder('lerato', 'mabena', 'mabena-cycle');
    expect(value.entries.map((e) => e.position), [1, 2, 3]);
    expect(value.nextMemberId, 'member-2');
  });
  test('a cycle with no order is empty not data', () async {
    final value = await repo.loadOrder('sipho', 'ubuntu', 'ubuntu-cycle');
    expect(payoutViewState(AsyncData(value)), isA<ViewEmpty<PayoutOrder>>());
  });
}
