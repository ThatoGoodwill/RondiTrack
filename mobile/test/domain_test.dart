import 'package:flutter_test/flutter_test.dart';
import 'package:ronditrack_mobile/features/home/data/fake_home_repository.dart';
import 'package:ronditrack_mobile/features/stokvel/domain/actions.dart';
import 'package:ronditrack_mobile/features/stokvel/domain/statuses.dart';

void main() {
  test('status unknown is safe when API adds a spelling', () {
    expect(CycleStatus.fromApi('Open'), CycleStatus.open);
    expect(CycleStatus.fromApi('Closed'), CycleStatus.closed);
    expect(CycleStatus.fromApi('PaidOut'), CycleStatus.paidOut);
    expect(CycleStatus.fromApi('FutureCycle'), CycleStatus.unknown);
    expect(StokvelRole.fromApi('Treasurer'), StokvelRole.treasurer);
    expect(StokvelRole.fromApi('FutureRole'), StokvelRole.unknown);
  });
  test('one identity has a Treasurer role in one stokvel, Member in another', () async {
    const repo = FakeHomeRepository(delay: Duration.zero);
    final list = await repo.loadHome('lerato');
    expect(list.where((e) => e.membership.role == StokvelRole.member).length, 1);
    expect(list.where((e) => e.membership.role == StokvelRole.treasurer).length, 1);
  });
  test('roles and status decide actions, including unknown', () {
    expect(permittedActions((StokvelRole.member, CycleStatus.open,
      ContributionStatus.notRecorded)).contains(StokvelAction.pay), isTrue);
    expect(permittedActions((StokvelRole.member, CycleStatus.open,
      ContributionStatus.recorded)).contains(StokvelAction.closeCycle), isFalse);
    expect(permittedActions((StokvelRole.treasurer, CycleStatus.open,
      ContributionStatus.notRecorded)).contains(StokvelAction.closeCycle), isTrue);
    expect(permittedActions((StokvelRole.unknown, CycleStatus.unknown,
      ContributionStatus.unknown)), isEmpty);
  });
  test('a foreign stokvel id looks like a nonexistent stokvel', () async {
    const repo = FakeHomeRepository(delay: Duration.zero);
    await expectLater(repo.loadDetail('nomsa', 'ubuntu'), throwsA(isA<StokvelNotFound>()));
    await expectLater(repo.loadDetail('lerato', 'does-not-exist'), throwsA(isA<StokvelNotFound>()));
  });
}
