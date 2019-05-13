using NationalInstruments.SystemLink.Clients.File;
using NationalInstruments.SystemLink.Clients.TestMonitor;
using System;
using System.Collections.Generic;

namespace TestGenerator
{
    class Program
    {
        private static bool AcceptAllCertifications(object sender, System.Security.Cryptography.X509Certificates.X509Certificate certification, System.Security.Cryptography.X509Certificates.X509Chain chain, System.Net.Security.SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        private static StepData GenerateStepData(string name, string stepType, double current, double voltage, double power, double lowLimit, double highLimit)
        {
            Random random = new Random();
            var status = new Status(StatusType.Running);
            var inputs = new List<NamedValue>();

            var outputs = new List<NamedValue>();

            var parameters = new List<Dictionary<string, string>>();
            if (stepType.Equals("NumericLimit"))
            {
                var parameter = new Dictionary<string, string>();
                parameter.Add("name", "Power Test");
                parameter.Add("status", "status");
                parameter.Add("measurement", $"{power}");
                parameter.Add("units", null);
                parameter.Add("nominalValue", null);
                parameter.Add("lowLimit", $"{lowLimit}");
                parameter.Add("highLimit", $"{highLimit}");
                parameter.Add("comparisonType", "GELT");
                parameters.Add(parameter);


                inputs = new List<NamedValue>()
                    {
                        new NamedValue("current", current),
                        new NamedValue("voltage", voltage)
                    };

                outputs = new List<NamedValue>()
                    {
                        new NamedValue("power", power)
                    };

                if (power < lowLimit || power > highLimit)
                {
                    status = new Status(StatusType.Failed);
                }
                else
                {
                    status = new Status(StatusType.Passed);
                }
            }

            var stepData = new StepData()
            {
                Inputs = inputs,
                Name = name,
                Outputs = outputs,
                StepType = stepType,
                Status = status,
                TotalTimeInSeconds = random.NextDouble() * 10,
                Parameters = parameters,
                DataModel = "TestStand",
            };


            return stepData;
        }

        static void Main(string[] args)
        {
            System.Net.ServicePointManager.ServerCertificateValidationCallback = new System.Net.Security.RemoteCertificateValidationCallback(AcceptAllCertifications);
            var serverUri = new Uri("https://ip-10-192-40-62.aws.natinst.com");
            //var username = "my-username";
            //var password = "my-password";
            //var httpConfiguration = new HttpConfiguration(serverUri, username, password);
            var httpTestDataManager = new HttpTestDataManager();
            var httpFileUploader = new HttpFileUploader();

            var lowLimit = 0;
            var highLimit = 70;

            for (var resultIndex = 0; resultIndex < 10; resultIndex++)
            {
                var fileId = httpFileUploader.UploadFile("C:\\Broadway.tdms");
                var resultData = new ResultData()
                {
                    Operator = "mvaterla",
                    ProgramName = "A C# App",
                    Status = new Status(StatusType.Running),
                    SerialNumber = Guid.NewGuid().ToString(),
                    Product = "Some Software",
                    FileIds = new List<string> { fileId }
                };
                var testResult = httpTestDataManager.CreateResult(resultData);
                testResult.AutoUpdateTotalTime = true;

                Random random = new Random();
                var stepDatas = new List<StepData>();
                var current = 0;
                var voltage = 0;
                var currentLoss = 1 - random.NextDouble();
                var voltageLoss = 1 - random.NextDouble();
                var power = current * currentLoss * voltage * voltageLoss;
                for (current = 0; current < 10; current++)
                {
                    currentLoss = 1 - random.NextDouble() * 0.25;
                    power = current * currentLoss * voltage * voltageLoss;
                    var currentStepData = GenerateStepData($"Current Sweep {current}A", "SequenceCall", current, voltage, power, lowLimit, highLimit);
                    var currentStep = testResult.CreateStep(currentStepData);

                    for (voltage = 0; voltage < 10; voltage++)
                    {
                        voltageLoss = 1 - random.NextDouble() * 0.25;
                        power = current * currentLoss * voltage * voltageLoss;
                        var voltageStepData = GenerateStepData($"Voltage Sweep {voltage}V", "NumericLimit", current, voltage, power, lowLimit, highLimit);
                        var voltageStep = currentStep.CreateStep(voltageStepData);

                        if (voltageStep.Data.Status.StatusType.Equals(StatusType.Failed))
                        {
                            currentStepData.Status = new Status(StatusType.Failed);
                            currentStep.Update(currentStepData);
                        }
                    }

                    if (currentStepData.Status.StatusType.Equals(StatusType.Running))
                    {
                        currentStepData.Status = new Status(StatusType.Passed);
                        currentStep.Update(currentStepData);
                    }
                }
                testResult = testResult.DetermineStatusFromSteps();
            }
        }
    }
}
