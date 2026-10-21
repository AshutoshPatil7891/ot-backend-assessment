using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OT.Assessment.Common.Settings
{

    public class RabbitMqSettings
    {
        public string HostName { get; set; } = "localhost";
        public int Port { get; set; } = 5672;
        public string UserName { get; set; } = "guest";
        public string Password { get; set; } = "guest";
        public string QueueName { get; set; } = "casino-bets";
        public string DeadLetterQueueName { get; set; } = "casino-bets.dead";
        // how many channels the API publishes over at the same time (each one sends one bet at a time)
        public int PublishChannels { get; set; } = 16;
    }


}
