using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace devBolseBotEnterprise.Dialogs.QnA
{
	public class FeedbackRecords
	{
		// <summary>
		/// List of feedback records
		/// </summary>
		[JsonProperty("feedbackRecords")]
		public FeedbackRecord[] Records { get; set; }
	}
}
