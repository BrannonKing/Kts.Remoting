using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

namespace Kts.Remoting.Benchmarks
{
	[DataContract]
	public sealed class SumServiceTree
	{
		[DataMember(Order = 1)]
		public long Leaf {get;set;}
		
		[DataMember(Order = 2)]
		public SumServiceTree[] Children{get;set;}

		public static void FillTree(SumServiceTree tree, Random rand, int level = 3)
		{
			tree.Leaf = rand.Next(1000000000, 2000000000);

			if (level <= 0)
				return;

			tree.Children = new SumServiceTree[300];
			for (int i = 0; i < tree.Children.Length; i++)
			{
				tree.Children[i] = new SumServiceTree();
				FillTree(tree.Children[i], rand, level - 1);
			}
		}

		public bool IsExactMatch(SumServiceTree tree, int diff)
		{
			if (Leaf + diff != tree.Leaf)
				return false;

			if (Children == null)
				return true;

			for (int i = 0; i < Children.Length; i++)
			{
				if (!Children[i].IsExactMatch(tree.Children[i], diff))
					return false;
			}
			return true;
		}
	}

	[DataContract]
	public class SumPackage
	{
		[DataMember(Order=1)]
		public int[] Numbers { get; set; }
	}

	public interface ISumService
	{
		Task<Tuple<long, long>> TimeDiff(long stamp);
		Task<int> SumPackage(SumPackage package);
		Task<int> Sum(int[] values);
		Task<SumServiceTree> Increment(SumServiceTree tree);
	}

	public class SumService : ISumService
	{
		public Task<Tuple<long, long>> TimeDiff(long stamp)
		{
			var current = Stopwatch.GetTimestamp();
			return Task.FromResult(Tuple.Create(current - stamp, current));
		}

		public Task<int> SumPackage(SumPackage package)
		{
			return Sum(package.Numbers);
		}

		public Task<int> Sum(int[] values)
		{
			return Task.FromResult(values.Sum());
		}

		public Task<SumServiceTree> Increment(SumServiceTree tree)
		{
			if (tree == null) return null;
			tree.Leaf++;
			if (tree.Children != null)
			{
				foreach (var child in tree.Children)
					Increment(child);
			}
			return Task.FromResult(tree);
		}
	}
}
