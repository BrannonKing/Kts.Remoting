using System;
using System.IO;
using System.Threading.Tasks;
using WebSocketSharp.Server;

// ReSharper disable once CheckNamespace
namespace Kts.Remoting.Shared
{
	public class WebSocketSharpServerBehavior : WebSocketBehavior
	{
		private readonly WebSocketSharpServerTransportSource _source;

		public WebSocketSharpServerBehavior(WebSocketSharpServerTransportSource source)
		{
			_source = source;
		}

		protected override void OnMessage(WebSocketSharp.MessageEventArgs e)
		{
			var args = new DataReceivedArgs
			{
				Data = new ArraySegment<byte>(e.RawData, 0, e.RawData.Length),
				SessionID = this
			};
			_source.FireRecieved(args);
		}

		public Task Send(ArraySegment<byte> data)
		{
			var source = new TaskCompletionSource<bool>();
			var ms = new MemoryStream(data.Array, data.Offset, data.Count, false);
			SendAsync(ms, data.Count, success =>
			{
				ms.Dispose();
				if (success)
					source.SetResult(true);
				else
					source.SetException(new Exception("Unable to send all the data."));
			});
			return source.Task;
		}
	}
}