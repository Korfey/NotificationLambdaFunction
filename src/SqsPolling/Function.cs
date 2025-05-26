using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using System.Text.Json;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace SqsPolling
{
    public record MetadataSqsDto(string Name, long ImageSize, string FileExtension, DateTime LastUpdate, string DownloadLink);

    public class Function
    {
        private static readonly IAmazonSimpleNotificationService snsClient = new AmazonSimpleNotificationServiceClient();
        private readonly string SnsArn = Environment.GetEnvironmentVariable("SnsArn")!;

        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {

        }


        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an SQS event object and can be used 
        /// to respond to SQS messages.
        /// </summary>
        /// <param name="evnt">The event for the Lambda function handler to process.</param>
        /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
        /// <returns></returns>
        public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
        {
            foreach (var message in evnt.Records)
            {
                await ProcessMessageAsync(message, context);
            }
        }

        private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
        {
            try
            {
                var metadata = JsonSerializer.Deserialize<MetadataSqsDto>(message.Body);
                string notificationMessage = BuildResponse(metadata);

                var publishRequest = BuildRequest(metadata, notificationMessage);

                await snsClient.PublishAsync(publishRequest);

            }
            catch (Exception)
            {
                context.Logger.LogError($"Message was not processed: {message?.Body ?? "NULL"}");
            }
            finally
            {
                context.Logger.LogInformation($"Processed message {message.Body}");
            }

            await Task.CompletedTask;
        }

        private PublishRequest BuildRequest(MetadataSqsDto? metadata, string notificationMessage)
        {
            return new PublishRequest
            {
                TopicArn = SnsArn,
                Message = notificationMessage,
                MessageAttributes = new Dictionary<string, Amazon.SimpleNotificationService.Model.MessageAttributeValue>
            {
                {
                    "imageExtension", new Amazon.SimpleNotificationService.Model.MessageAttributeValue
                    {
                        DataType = "String",
                        StringValue = metadata.FileExtension
                    }
                }
            }
            };
        }

        private string BuildResponse(MetadataSqsDto? metadata)
        {
            return
                $"""
                New image uploaded to S3 bucket.
                    Image Name: {metadata.Name}
                    Image Size: {metadata.ImageSize} bytes
                    Image Extension: {metadata.FileExtension}
                    Image Upload Date: {metadata.LastUpdate}
                Download link: {metadata.DownloadLink}
                """;
        }
    }
}