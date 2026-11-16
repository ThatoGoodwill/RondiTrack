import '../../home/domain/home_repository.dart';
import '../../home/domain/not_found.dart';
import '../../../core/money/cents.dart';
import '../domain/payout_repository.dart';
import '../domain/payout_models.dart';

/// The server has no GET payout ORDER yet. This fixture is a proposed contract,
/// NOT computed from money or membership rules in the Flutter application.
class FakePayoutRepository implements PayoutRepository {
  const FakePayoutRepository(this.homeRepository);
  final HomeRepository homeRepository;

  @override
  Future<PayoutOrder> loadOrder(String persona, String stokvelId, String cycleId) async {
    final details = await homeRepository.loadDetail(persona, stokvelId);
    if (details.home.currentCycle?.id != cycleId) throw const StokvelNotFound();
    if (stokvelId == 'ubuntu') {
      return PayoutOrder(stokvelId: stokvelId, cycleId: cycleId, entries: const []);
    }
    // Deliberately use server-like seed order; never sort or calculate the next person.
    return PayoutOrder(stokvelId: stokvelId, cycleId: cycleId,
      nextMemberId: 'member-2', entries: const [
        PayoutOrderEntry(memberId: 'member-1', memberName: 'Member 1',
          position: 1, amountCents: Cents(35050), status: PayoutStatus.paid),
        PayoutOrderEntry(memberId: 'member-2', memberName: 'Member 2',
          position: 2, amountCents: Cents(35050), status: PayoutStatus.next),
        PayoutOrderEntry(memberId: 'member-3', memberName: 'Member 3',
          position: 3, amountCents: Cents(35050), status: PayoutStatus.waiting),
      ]);
  }
}
