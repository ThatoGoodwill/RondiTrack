import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../network/failure.dart';

/// The same readable presentation for all network errors.
class FailureMessage extends StatelessWidget {
  const FailureMessage({required this.error, this.retry, super.key});
  final Object error;
  final VoidCallback? retry;

  @override
  Widget build(BuildContext context) {
    final failure = error is Failure
      ? error as Failure : const UnexpectedFailure();
    return Center(child: Padding(
      padding: const EdgeInsets.all(20),
      child: Column(mainAxisSize: MainAxisSize.min, children: [
        Text(failure.message, textAlign: TextAlign.center),
        if (failure.reference case final reference?) ...[
          const SizedBox(height: 8),
          SelectableText('Reference: $reference'),
          TextButton.icon(
            onPressed: () => Clipboard.setData(ClipboardData(text: reference)),
            icon: const Icon(Icons.copy),
            label: const Text('Copy reference')),
        ],
        if (failure.canRetry && retry != null) ...[
          const SizedBox(height: 8),
          TextButton(onPressed: retry, child: const Text('Try again')),
        ],
      ]),
    ));
  }
}
