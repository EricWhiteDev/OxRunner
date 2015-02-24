using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Messaging;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OxRun
{
    public class OxRunConstants
    {
        public const string ControllerMasterQueueName = "controllermaster";
        public const string ControllerMasterStatusQueueName = "controllermasterstatus";
        public const string ControllerMasterIsAliveQueueName = "controllermasteralive";
        public const string ControllerDaemonQueueName = "controllerdaemon";
        public const string RunnerDaemonQueueName = "runnerdaemon"; // minor version of the assembly gets appended to this
        public const int RunnerDaemonProcessesPerClient = 5;

        public const string RunnerMasterQueueName = "runnermaster";
        public const string RunnerMasterIsAliveQueueName = "runnermasteralive";
    }

    public class OxMessage
    {
        public string Label;
        public XElement Xml;
        public bool Timeout;
        public int MessageSize;
    }

    public class Runner
    {
        public static string GetQueueName(string machineName, string queueName)
        {
            if (machineName.ToLower() == Environment.MachineName.ToLower())
                return string.Format(@".\PRIVATE$\{0}", queueName);
            return string.Format(@"FormatName:Direct=OS:{0}\PRIVATE$\{1}", machineName.ToLower(), queueName);
        }

        public static void ClearQueue(MessageQueue messageQueue)
        {
            while (true)
            {
                var rx = ReceiveMessage(messageQueue, 1);
                if (rx.Timeout)
                    break;
            }
        }

        public static OxMessage ReceiveMessage(MessageQueue messageQueue, int? timeout)
        {
            // Console.WriteLine("Receiving message from queue: {0}", messageQueue.QueueName);
            Message message;
            var oxMessage = new OxMessage();

            try
            {
                if (timeout != null)
                    message = messageQueue.Receive(new TimeSpan(0, 0, (int)timeout));
                else
                    message = messageQueue.Receive();
            }
            catch (MessageQueueException /* mqe */)
            {
                // Console.WriteLine(mqe.ToString());
                oxMessage.Timeout = true;
                return oxMessage;
            }
            oxMessage.Timeout = false;
            message.Formatter = new XmlMessageFormatter(new String[] { "System.String,mscorlib" });
            oxMessage.Label = message.Label;
            var text = message.Body.ToString(); 
            oxMessage.Xml = XElement.Parse(text);
            oxMessage.MessageSize = text.Length;
            return oxMessage;
        }

        public static string SendMessage(string label, XElement xml, string machineName, string queueName)
        {
            Message message = new Message();
            message.Label = label;
            message.Body = xml.ToString(SaveOptions.DisableFormatting);
            var qualifiedQueueName = GetQueueName(machineName, queueName);
            // Console.WriteLine("Sending {0} to queue {1}", label, qualifiedQueueName);
            var queue = new MessageQueue(qualifiedQueueName);
            queue.Send(message);
            return message.Id;
        }
    }

}
