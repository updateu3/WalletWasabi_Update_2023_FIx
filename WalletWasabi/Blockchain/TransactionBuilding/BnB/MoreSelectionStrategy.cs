namespace WalletWasabi.Blockchain.TransactionBuilding.BnB;

/// <summary>
/// Strategy that searches search-space and caches every found selection that minimizes
/// waste of user's fund by looking for a selection that minimizes inputs' spending costs
/// and extra cost of paying more than specified target.
/// </summary>
public class MoreSelectionStrategy : SelectionStrategy
{
	/// <inheritdoc/>
	public MoreSelectionStrategy(long target, long[] inputValues, long[] inputCosts) : base(target, inputValues, inputCosts)
	{
		BestTargetSoFar = long.MaxValue;
	}

	public override EvaluationResult Evaluate(long[] selection, int depth, long sum)
	{
		long totalCost = sum + CurrentInputCosts;

		if (totalCost > BestTargetSoFar)
		{
			// Our solution is already better than what we might get here.
			return EvaluationResult.SkipBranch;
		}
		else if (sum >= Target)
		{
			if (BestTargetSoFar > totalCost)
			{
				BestSelectionSoFar = selection[0..depth];
				BestTargetSoFar = totalCost;
			}

			// Even if a match occurred we cannot be sure that there isn't
			// a better selection thanks to input costs.
			return EvaluationResult.SkipBranch;
		}
		else if (sum + RemainingAmounts[depth - 1] < Target)
		{
			// The remaining coins cannot sum up to required target, cut the branch.
			return EvaluationResult.SkipBranch;
		}
		else if (depth == selection.Length)
		{
			// Leaf reached, no match
			return EvaluationResult.SkipBranch;
		}

		return EvaluationResult.Continue;
	}
}
