import 'package:dio/dio.dart';
import '../../../core/money/cents.dart';
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
      final json = Map<String, dynamic>.from(response.data as Map);
      final entries = (json['entries'] as List).map((value) {
        final row = Map<String, dynamic>.from(value as Map);
        return PayoutOrderEntry(
          memberId: row['memberId'] as String,
          memberName: row['memberName'] as String,
          position: (row['position'] as num).toInt(),
          amountCents: Cents((row['amountCents'] as num).toInt()),
          status: PayoutStatus.fromApi(row['status'] as String),
          paidOnUtc: row['paidOnUtc'] as String?,
        );
      }).toList(growable: false);
      return PayoutOrder(
        stokvelId: json['stokvelId'] as String,
        cycleId: json['cycleId'] as String,
        nextMemberId: json['nextMemberId'] as String?,
        entries: entries);
    } catch (error) {
      throw mapFailure(error);
    }
  }
}
