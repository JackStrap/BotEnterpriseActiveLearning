using Microsoft.Bot.Schema;
using System.Collections.Generic;

namespace devBolseBotEnterprise.Dialogs.QnA
{
	public class QnACard
	{
		/// <summary>
		/// Get Hero card
		/// </summary>
		/// <param name="suggestionsList">List of suggested questions</param>
		/// <param name="cardTitle">Title of the cards</param>
		/// <param name="cardNoMatchText">No match text</param>
		/// <returns></returns>
		public static IMessageActivity GetHeroCard(List<string> suggestionsList, string cardTitle = "Vouliez-vous dire:", string cardNoMatchText = "Aucune de ces réponses.")
		{
			IMessageActivity chatActivity = Activity.CreateMessageActivity();
			List<CardAction> buttonList = new List<CardAction>();

			// Add all suggestions
			foreach (var suggestion in suggestionsList)
			{
				buttonList.Add(
					new CardAction()
					{
						Value = suggestion,
						Type = "imBack",
						Title = suggestion,
					});
			}

			// Add No match text
			buttonList.Add(
				new CardAction()
				{
					Value = cardNoMatchText,
					Type = "imBack",
					Title = cardNoMatchText
				});

			var plCard = new HeroCard()
			{
				Title = cardTitle,
				Subtitle = string.Empty,
				Buttons = buttonList
			};

			// Create the attachment.
			Attachment attachment = plCard.ToAttachment();

			chatActivity.Attachments.Add(attachment);

			return chatActivity;
		}
	}
}
