import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:ronditrack_mobile/core/network/failure.dart';
import 'package:ronditrack_mobile/core/widgets/failure_message.dart';

void main() {
  Future<void> show(WidgetTester tester, Failure failure, {VoidCallback? retry}) =>
      tester.pumpWidget(MaterialApp(home: Scaffold(body:
        FailureMessage(error: failure, retry: retry))));

  testWidgets('non-member 404 always says Not found', (tester) async {
    await show(tester, const NotFoundFailure());
    expect(find.text('Not found'), findsOneWidget);
    expect(find.text('Try again'), findsNothing);
  });

  testWidgets('member 403 keeps a readable permission message', (tester) async {
    await show(tester, const ForbiddenFailure());
    expect(find.text('You do not have permission to do that.'), findsOneWidget);
  });

  testWidgets('rate limited response shows the API wait', (tester) async {
    await show(tester, const RateLimitedFailure(37));
    expect(find.textContaining('37 seconds'), findsOneWidget);
  });

  testWidgets('server issue shows copyable correlation reference', (tester) async {
    await show(tester, const ServerFailure(reference: 'trace-123'));
    expect(find.textContaining('Something went wrong on our side'), findsOneWidget);
    expect(find.text('Reference: trace-123'), findsOneWidget);
    expect(find.text('Copy reference'), findsOneWidget);
    expect(find.text('Try again'), findsNothing);
  });

  testWidgets('only offline shows the retry action', (tester) async {
    var attempts = 0;
    await show(tester, const OfflineFailure(), retry: () => attempts++);
    expect(find.textContaining("You're offline"), findsOneWidget);
    await tester.tap(find.text('Try again'));
    expect(attempts, 1);
  });
}
