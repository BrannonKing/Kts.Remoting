using System;
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

	}

	public interface ISumService
	{
		Task<int> Sum(int[] values);
		Task<SumServiceTree> Increment(SumServiceTree tree);
	}

	public class SumService : ISumService
	{
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
