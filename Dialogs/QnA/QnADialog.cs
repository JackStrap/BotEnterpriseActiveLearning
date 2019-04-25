using devBolseBotEnterprise.Dialogs.Shared;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Builder.AI.QnA;
using Microsoft.Bot.Builder.Dialogs;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace devBolseBotEnterprise.Dialogs.QnA
{
	public class QnADialog : EnterpriseDialog
	{
		private readonly string QnAMakerKey;
		private readonly BotServices _services;

		private const string CurrentQuery = "value-current-query";
		private const string QnAData = "value-qnaData";

		// Dialog Options parameters
		private const float DefaultThreshold = 0.03F;
		private const int DefaultTopN = 3;

		private QnAResponses _responder = new QnAResponses();

		public QnADialog(BotServices botServices, string qnAMakerKey) : base(botServices, nameof(QnADialog))
		{
			InitialDialogId = nameof(QnADialog);

			WaterfallStep[] qNa = new WaterfallStep[]
			{
				CallGenerateAnswer,
				FilterLowVariationScoreList,
				GetResponse,
				DisplayQnAResult
			};

			this._services = botServices;
			this.QnAMakerKey = qnAMakerKey;

			AddDialog(new WaterfallDialog(InitialDialogId, qNa));
		}

		private async Task<DialogTurnResult> CallGenerateAnswer(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
			float scoreThreshold = DefaultThreshold;
			int top = DefaultTopN;

			QnAMakerOptions qnaMakerOptions = null;

			// Getting options
			if (stepContext.ActiveDialog.State["options"] != null)
			{
				qnaMakerOptions = stepContext.ActiveDialog.State["options"] as QnAMakerOptions;
				scoreThreshold = qnaMakerOptions?.ScoreThreshold != null ? qnaMakerOptions.ScoreThreshold : DefaultThreshold;
				top = qnaMakerOptions?.Top != null ? qnaMakerOptions.Top : DefaultTopN;
			}

			QueryResult[] response = await _services.QnAServices[QnAMakerKey].GetAnswersAsync(stepContext.Context, qnaMakerOptions);

			List<QueryResult> filteredResponse = response.Where(answer => answer.Score > scoreThreshold).ToList();

			stepContext.Values[QnAData] = new List<QueryResult>(filteredResponse);
			stepContext.Values[CurrentQuery] = stepContext.Context.Activity.Text;

			return await stepContext.NextAsync(cancellationToken);
		}

		private async Task<DialogTurnResult> FilterLowVariationScoreList(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
			List<QueryResult> response = stepContext.Values[QnAData] as List<QueryResult>;

			List<QueryResult> filteredResponse = QnAResponses.GetLowScoreVariation(response);

			stepContext.Values[QnAData] = filteredResponse;

			if (filteredResponse.Count > 1)
			{
				List<string> suggestedQuestions = new List<string>();
				foreach (var qna in filteredResponse)
				{
					suggestedQuestions.Add(qna.Questions[0]);
				}

				// Get hero card activity
				IMessageActivity message = QnACard.GetHeroCard(suggestedQuestions);

				await stepContext.Context.SendActivityAsync(message);

				return new DialogTurnResult(DialogTurnStatus.Waiting);
			}
			else
			{
				return await stepContext.NextAsync(new List<QueryResult>(response), cancellationToken);
			}
		}

		private async Task<DialogTurnResult> GetResponse(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
			List<QueryResult> userResponses = stepContext.Values[QnAData] as List<QueryResult>;
			string currentQuery = stepContext.Values[CurrentQuery] as string;

			if (userResponses.Count > 1)
			{
				string reply = stepContext.Context.Activity.Text;
				QueryResult qnaResult = userResponses.Where(kvp => kvp.Questions[0] == reply).FirstOrDefault();

				if (qnaResult != null)
				{
					stepContext.Values[QnAData] = new List<QueryResult>() { qnaResult };

					return await stepContext.NextAsync(new List<QueryResult>() { qnaResult }, cancellationToken);
				}
				else
				{
					return await stepContext.EndDialogAsync();
				}
			}

			return await stepContext.NextAsync(stepContext.Result, cancellationToken);
		}

		private async Task<DialogTurnResult> DisplayQnAResult(WaterfallStepContext stepContext, CancellationToken cancellationToken)
		{
			if (stepContext.Result is List<QueryResult> response && response.Count > 0)
			{
				await stepContext.Context.SendActivityAsync(response[0].Answer, cancellationToken: cancellationToken);
			}
			else
			{
				string msg = "Aucune réponse n'a été trouvée.";
				await stepContext.Context.SendActivityAsync(msg, cancellationToken: cancellationToken);
			}

			return await stepContext.EndDialogAsync();
		}
	}
}
