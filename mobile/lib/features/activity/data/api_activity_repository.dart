import 'package:dio/dio.dart';
import '../../../core/network/failure_mapper.dart';
import '../../../core/network/data/wire_models.dart';

class ApiActivityRepository {
  const ApiActivityRepository(this.dio);
  final Dio dio;

  Future<List<String>> firstPage() async {
    try {
      final response = await dio.get<dynamic>('/api/v1/activity',
        queryParameters: {'pageSize': 10});
      final page = ActivityPageWire.fromJson(
          Map<String, dynamic>.from(response.data as Map));
      return page.items.map((item) => item.description).toList(growable: false);
    } catch (error) {
      throw mapFailure(error);
    }
  }
}
