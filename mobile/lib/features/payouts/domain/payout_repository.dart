import 'payout_models.dart';
abstract interface class PayoutRepository {
  Future<PayoutOrder> loadOrder(String persona, String stokvelId, String cycleId);
}
