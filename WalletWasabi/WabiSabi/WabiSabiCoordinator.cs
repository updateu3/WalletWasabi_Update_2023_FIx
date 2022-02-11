using Microsoft.Extensions.Hosting;
using NBitcoin;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WalletWasabi.BitcoinCore.Rpc;
using WalletWasabi.Logging;
using WalletWasabi.Services;
using WalletWasabi.WabiSabi.Backend;
using WalletWasabi.WabiSabi.Backend.Banning;
using WalletWasabi.WabiSabi.Backend.Rounds;
using WalletWasabi.WabiSabi.Backend.Rounds.CoinJoinStorage;
using WalletWasabi.WabiSabi.Backend.Statistics;

namespace WalletWasabi.WabiSabi;

public class WabiSabiCoordinator : BackgroundService
{
	public WabiSabiCoordinator(CoordinatorParameters parameters, IRPCClient rpc)
	{
		Parameters = parameters;

		Warden = new(parameters.UtxoWardenPeriod, parameters.PrisonFilePath, Config);
		ConfigWatcher = new(parameters.ConfigChangeMonitoringPeriod, Config, () => Logger.LogInfo("WabiSabi configuration has changed."));

		CoinJoinTransactionArchiver transactionArchiver = new(Path.Combine(parameters.CoordinatorDataDir, "CoinJoinTransactions"));

		CoinJoinFeeRateStatStore = CoinJoinFeeRateStatStore.LoadFromFile(parameters.CoinJoinFeeRateStatStoreFilePath, Config, rpc);
		CoinJoinFeeRateStatStore.NewStat += FeeRateStatStore_NewStat;

		var inMemoryCoinJoinIdStore = InMemoryCoinJoinIdStore.LoadFromFile(parameters.CoinJoinIdStoreFilePath);

		Arena = new(parameters.RoundProgressSteppingPeriod, rpc.Network, Config, rpc, Warden.Prison, inMemoryCoinJoinIdStore, transactionArchiver);
		Arena.CoinJoinBroadcast += Arena_CoinJoinBroadcast;
	}

	public ConfigWatcher ConfigWatcher { get; }
	public Warden Warden { get; }

	public CoordinatorParameters Parameters { get; }
	public Arena Arena { get; }

	public CoinJoinFeeRateStatStore CoinJoinFeeRateStatStore { get; }

	public WabiSabiConfig Config => Parameters.RuntimeCoordinatorConfig;

	private void Arena_CoinJoinBroadcast(object? sender, Transaction e)
	{
		if (!File.Exists(Parameters.CoinJoinIdStoreFilePath))
		{
			IoHelpers.EnsureContainingDirectoryExists(Parameters.CoinJoinIdStoreFilePath);
		}

		File.AppendAllLines(Parameters.CoinJoinIdStoreFilePath, new[] { e.GetHash().ToString() });
	}

	private void FeeRateStatStore_NewStat(object? sender, CoinJoinFeeRateStat feeRateStat)
	{
		if (!File.Exists(Parameters.CoinJoinFeeRateStatStoreFilePath))
		{
			IoHelpers.EnsureContainingDirectoryExists(Parameters.CoinJoinFeeRateStatStoreFilePath);
		}

		File.AppendAllLines(Parameters.CoinJoinFeeRateStatStoreFilePath, new[] { feeRateStat.ToLine() });
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await ConfigWatcher.StartAsync(stoppingToken).ConfigureAwait(false);
		await Warden.StartAsync(stoppingToken).ConfigureAwait(false);
		await Arena.StartAsync(stoppingToken).ConfigureAwait(false);

		await CoinJoinFeeRateStatStore.StartAsync(stoppingToken).ConfigureAwait(false);
	}

	public override async Task StopAsync(CancellationToken cancellationToken)
	{
		await base.StopAsync(cancellationToken).ConfigureAwait(false);

		await Arena.StopAsync(cancellationToken).ConfigureAwait(false);
		await ConfigWatcher.StopAsync(cancellationToken).ConfigureAwait(false);
		await Warden.StopAsync(cancellationToken).ConfigureAwait(false);

		await CoinJoinFeeRateStatStore.StopAsync(cancellationToken).ConfigureAwait(false);
	}

	public override void Dispose()
	{
		CoinJoinFeeRateStatStore.NewStat -= FeeRateStatStore_NewStat;
		Arena.CoinJoinBroadcast -= Arena_CoinJoinBroadcast;
		ConfigWatcher.Dispose();
		Warden.Dispose();
		base.Dispose();
	}
}
