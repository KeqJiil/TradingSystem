using Messaging.Serializers;

namespace Messaging.IntegrationTests.Support;

public static class OrdersHost
{
    public static Task<MessagingTestHost> StartAsync(KafkaFixture kafka, ITestOutputHelper output, string topic,
        string group, ConsumerRecorder<OrderPlaced> recorder, Action<MessagingOptions>? configureOptions = null,
        DeliveryCaptureLog? capture = null, Action<MessagingBuilder>? configureMessaging = null)
    {
        return MessagingTestHost.StartAsync(kafka, output,
            services =>
            {
                services.AddSingleton(recorder);
                if (capture is not null) services.AddSingleton(capture);
            },
            messaging =>
            {
                messaging
                    .AddMessage<OrderPlaced>(topic, new JsonDefaultSerializer())
                    .AddConsumer<OrderPlaced, ScriptedConsumer<OrderPlaced>>(new ConsumerOptions(topic, group));
                if (capture is not null) messaging.AddDeliveryMiddleware<CaptureDeliveryMiddleware>();
                configureMessaging?.Invoke(messaging);
            },
            configureOptions);
    }

    public static string DlqTopic(string topic, string group)
    {
        return $"{group}.{topic}.dlq";
    }
}
