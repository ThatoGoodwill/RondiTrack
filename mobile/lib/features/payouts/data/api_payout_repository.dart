import 'package:dio/dio.dart';
import '../../../core/money/cents.dart';
import '../../../core/network/data/wire_models.dart';
import '../../../core/network/failure_mapper.dart';
import '../domain/payout_models.dart';
import '../domain/payout_repository.dart';

/// Never sort the order or compute who is next: the API decides both.
class ApiPayoutRepository implements PayoutRepository {
  const ApiPayoutRepository(this.dio);
  final Dio dio;

  @override
  Future<PayoutOrder> loadOrder(String persona, String stokvelId, String cycleId) async {
    try {
      final response = await dio.get<dynamic>(
        '/api/v1/stokvels/${Uri.encodeComponent(stokvelId)}'
        '/cycles/${Uri.encodeComponent(cycleId)}/payout/order',
      );
      final wire = PayoutOrderWire.fromJson(
          Map<String, dynamic>.from(response.data as Map));
      return PayoutOrder(
        stokvelId: wire.stokvelId,
        cycleId: wire.cycleId,
        nextMemberId: wire.nextMemberId,
        entries: wire.entries.map((row) => PayoutOrderEntry(
          memberId: row.memberId,
          memberName: row.memberName,
          position: row.position,
          amountCents: Cents(row.amountCents),
          status: PayoutStatus.fromApi(row.status),
          paidOnUtc: row.paidOnUtc,
        )).toList(growable: false),
      );
    } catch (error) {
      throw mapFailure(error);
    }
  }
}
