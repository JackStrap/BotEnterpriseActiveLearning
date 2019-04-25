using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace devBolseBotEnterprise.Dialogs.QnA
{
	public class FeedbackRecord
	{
		/// <summary>
		/// User id
		/// </summary>
		public string UserId { get; set; }

		/// <summary>
		/// User question
		/// </summary>
		public string UserQuestion { get; set; }

		/// <summary>
		/// QnA Id
		/// </summary>
		public int QnaId { get; set; }
	}
}
