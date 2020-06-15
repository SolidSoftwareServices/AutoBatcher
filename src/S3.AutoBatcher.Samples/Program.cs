using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using S3.AutoBatcher.Samples.SimpleProducer;
using Console = Colorful.Console;
namespace S3.AutoBatcher.Samples
{
	class Program
	{
		public class ProgramInputOptions
		{
			[Option('r',"run",Required = true,HelpText = "specifies the sample to run")]
			public  SampleName RunSample { get; set; }
		}

		public enum SampleName
		{
			Producer_BatchCollectionByIdleTime
		}

		static int Main(string[] args)
		{
			return Parser.Default.ParseArguments<ProgramInputOptions>(args)
				.MapResult(
					input=>ExecuteSample(input).GetAwaiter().GetResult(),
					HandleParseErrors);
			int HandleParseErrors(IEnumerable<Error> errs)
			{

				var result = -1;
				Console.WriteLine(String.Join(Environment.NewLine, errs.Select(x =>
				{
					switch (x)
					{

						case NamedError namedError:
							return $"{x.GetType().Name}, {namedError.NameInfo}";
						case TokenError tokenError:
							return $"{x.GetType().Name}, {tokenError.Token}";
						case HelpVerbRequestedError helpError:
							return $"{x.GetType().Name}, verb:{helpError.Verb} - type:{helpError.Type} - matched:{helpError.Matched} ";
						case InvalidAttributeConfigurationError invalidAttribute:
							return $"{x.GetType().Name}, {InvalidAttributeConfigurationError.ErrorMessage}";
						case MissingGroupOptionError missingGroupOption:
							return $"{x.GetType().Name}, {InvalidAttributeConfigurationError.ErrorMessage} group:{missingGroupOption.Group} names:{string.Join(Environment.NewLine, missingGroupOption.Names.Select(y => $"{y.ShortName} {y.LongName}"))}";
						case MultipleDefaultVerbsError error:
							return $"{x.GetType().Name}, {MultipleDefaultVerbsError.ErrorMessage}";
						default:
							return $"{x.GetType().Name}, ";
					}
				})));


				return result;
			}
		}

		private static async Task<int> ExecuteSample(ProgramInputOptions input)
		{
			try
			{
				ISample sample;
				switch (input.RunSample)
				{
					case SampleName.Producer_BatchCollectionByIdleTime:
						sample = new Sample();
						break;
					default:
						throw new ArgumentOutOfRangeException();
				}

				Console.WriteLine("Sample Description:",Color.GreenYellow);
				Console.WriteLine(sample.Description,Color.DeepSkyBlue);
				Console.WriteLine("Press key to run...");
				Console.ReadKey();
				await sample.Run();
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex,Color.Red);
				return 1;
			}
			return 0;
		}
	}

	interface ISample
	{
		Task Run();
		string Description { get; }
	}
}
