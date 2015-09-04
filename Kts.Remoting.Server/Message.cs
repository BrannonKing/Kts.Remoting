using CommonSerializer;
using System.Runtime.Serialization;

namespace Kts.Remoting.Server
{
	[DataContract]
	public sealed class Message
	{
		/// <summary>
		/// Unique to the process sending the message. Used to tie a response to the original message.
		/// </summary>
		[DataMember(Name = "I", Order = 1)]
		public string Id { get; set; }

		/// <summary>
		/// Name of the class to run the method on. It will be absent on a response.
		/// </summary>
		[DataMember(Name = "H", Order = 2)]
		public string Hub { get; set; }

		/// <summary>
		/// Name of the method in the hub to trigger. It could be an event.
		/// </summary>
		[DataMember(Name = "M", Order = 3)]
		public string Method { get; set; }

		/// <summary>
		/// Method parameters.
		/// </summary>
		[DataMember(Name = "A", Order = 4)]
		public ISerializedContainer Arguments { get; set; }

		/// <summary>
		/// Method return value.
		/// </summary>
		[DataMember(Name = "R", Order = 5)]
		public ISerializedContainer Results { get; set; }

		[DataMember(Name = "E", Order = 6)]
		public string Error { get; set; }

		[DataMember(Name = "T", Order = 7)]
		public string StackTrace { get; set; }

	}

	// from SignalR:
	//public class HubInvocation
	//{
	//	[JsonProperty("I")]
	//	public string CallbackId { get; set; }

	//	[JsonProperty("H")]
	//	public string Hub { get; set; }

	//	[JsonProperty("M")]
	//	public string Method { get; set; }

	//	[SuppressMessage("Microsoft.Performance", "CA1819:PropertiesShouldNotReturnArrays", Justification = "This type is used for serialization")]
	//	[JsonProperty("A")]
	//	public JToken[] Args { get; set; }

	//	[SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "This type is used for serialization")]
	//	[JsonProperty("S", NullValueHandling = NullValueHandling.Ignore)]
	//	public Dictionary<string, JToken> State { get; set; }
	//}
	// their ClientHubInvocation class is the same as the above minus the CallbackId; we'll just reuse this one

	//public class HubResult
	//{
	//	/// <summary>
	//	/// The callback identifier
	//	/// </summary>
	//	[JsonProperty("I")]
	//	public string Id { get; set; }

	//	/// <summary>
	//	/// The progress update of the invocation
	//	/// </summary>
	//	[JsonProperty("P")]
	//	public HubProgressUpdate ProgressUpdate { get; set; }

	//	/// <summary>
	//	/// The return value of the hub
	//	/// </summary>
	//	[JsonProperty("R")]
	//	public JToken Result { get; set; }

	//	/// <summary>
	//	/// Indicates whether the Error is a <see cref="HubException"/>.
	//	/// </summary>
	//	[JsonProperty("H")]
	//	public bool? IsHubException { get; set; }

	//	/// <summary>
	//	/// The error message returned from the hub invocation.
	//	/// </summary>
	//	[JsonProperty("E")]
	//	public string Error { get; set; }

	//	/// <summary>
	//	/// Extra error data
	//	/// </summary>
	//	[JsonProperty("D")]
	//	public object ErrorData { get; set; }

	//	/// <summary>
	//	/// The caller state from this hub.
	//	/// </summary>
	//	[SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "Type is used for serialization.")]
	//	[JsonProperty("S")]
	//	public IDictionary<string, JToken> State { get; set; }
	//}

	//public class HubResponse
	//{
	//	/// <summary>
	//	/// The changes made the the round tripped state.
	//	/// </summary>
	//	[SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly", Justification = "Type is used for serialization")]
	//	[JsonProperty("S", NullValueHandling = NullValueHandling.Ignore)]
	//	public IDictionary<string, object> State { get; set; }

	//	/// <summary>
	//	/// The result of the invocation.
	//	/// </summary>
	//	[JsonProperty("R", NullValueHandling = NullValueHandling.Ignore)]
	//	public object Result { get; set; }

	//	/// <summary>
	//	/// The id of the operation.
	//	/// </summary>
	//	[JsonProperty("I")]
	//	public string Id { get; set; }

	//	/// <summary>
	//	/// The progress update of the invocation.
	//	/// </summary>
	//	[JsonProperty("P", NullValueHandling = NullValueHandling.Ignore)]
	//	public object Progress { get; set; }

	//	/// <summary>
	//	/// Indicates whether the Error is a see <see cref="HubException"/>.
	//	/// </summary>
	//	[JsonProperty("H", NullValueHandling = NullValueHandling.Ignore)]
	//	public bool? IsHubException { get; set; }

	//	/// <summary>
	//	/// The exception that occurs as a result of invoking the hub method.
	//	/// </summary>
	//	[JsonProperty("E", NullValueHandling = NullValueHandling.Ignore)]
	//	public string Error { get; set; }

	//	/// <summary>
	//	/// The stack trace of the exception that occurs as a result of invoking the hub method.
	//	/// </summary>
	//	[JsonProperty("T", NullValueHandling = NullValueHandling.Ignore)]
	//	public string StackTrace { get; set; }

	//	/// <summary>
	//	/// Extra error data contained in the <see cref="HubException"/>
	//	/// </summary>
	//	[JsonProperty("D", NullValueHandling = NullValueHandling.Ignore)]
	//	public object ErrorData { get; set; }
	//}
}
