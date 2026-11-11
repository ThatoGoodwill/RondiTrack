import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/money/cents.dart';
import '../domain/models.dart';
import 'providers.dart';
import '../../stokvel/presentation/stokvel_screen.dart';

class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final person = ref.watch(selectedPersonaProvider);
    final home = ref.watch(memberHomeProvider);
    return Scaffold(
      appBar: AppBar(
        title: const Text('RondiTrack'),
        actions: [
          DropdownButton<String>(
            key: const Key('persona-picker'),
            value: person,
            items: const [
              DropdownMenuItem(value: 'lerato', child: Text('Lerato')),
              DropdownMenuItem(value: 'sipho', child: Text('Sipho')),
              DropdownMenuItem(value: 'nomsa', child: Text('Nomsa')),
            ],
            onChanged: (value) {
              if (value != null) ref.read(selectedPersonaProvider.notifier).select(value);
            },
          ),
        ],
      ),
      body: home.when(
        skipLoadingOnRefresh: true,
        loading: () => const Center(child: CircularProgressIndicator(key: Key('home-loading'))),
        error: (error, stack) => Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
          const Text('Could not load your stokvels'),
          TextButton(onPressed: () => ref.invalidate(memberHomeProvider), child: const Text('Try again')),
        ])),
        data: (entries) => entries.isEmpty
          ? const Center(child: Text('You do not belong to any stokvels yet. Ask for an invite.'))
          : RefreshIndicator(
              onRefresh: () async { await ref.refresh(memberHomeProvider.future); },
              child: ListView.builder(
                key: const Key('home-list'),
                itemCount: entries.length,
                itemBuilder: (context, index) => _card(context, entries[index]),
              ),
            ),
      ),
    );
  }

  Widget _card(BuildContext context, HomeEntry entry) {
    final cycle = entry.currentCycle;
    return Card(
      child: ListTile(
        title: Text(entry.name),
        subtitle: Text(
          entry.membership.role.label + '\n' +
          'Paid: ' + (cycle?.paidCents.formatZa() ?? '—') +
          ' · Owed: ' + (cycle?.owedCents.formatZa() ?? '—') +
          '\nArrears: ' + entry.arrearsCents.formatZa() +
          '\nNext payout: ' + (entry.nextPayoutDueUtc?.toIso8601String() ?? 'Not scheduled'),
        ),
        isThreeLine: true,
        trailing: const Icon(Icons.chevron_right),
        onTap: () => Navigator.of(context).push(MaterialPageRoute<void>(
          builder: (_) => StokvelScreen(initialId: entry.membership.stokvelId),
        )),
      ),
    );
  }
}
