import 'package:dio/dio.dart';
import '../../../core/network/failure_mapper.dart';

class ApiActivityRepository {
  const ApiActivityRepository(this.dio);
  final Dio dio;

  Future<List<String>> firstPage() async {
    try {
      final response = await dio.get<dynamic>('/api/v1/activity',
        queryParameters: {'pageSize': 10});
      final body = Map<String, dynamic>.from(response.data as Map);
      return (body['items'] as List).map((item) =>
        Map<String, dynamic>.from(item as Map)['description'] as String).toList();
    } catch (error) {
      throw mapFailure(error);
    }
  }
}
