using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using CommandLine;
using CommandLine.Text;
using HtmlAgilityPack;
using Newtonsoft.Json;

namespace GenericScraper.Console
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			//if (args.Length == 0)
			//{
			//	Parser.Default.ParseArguments(new string[0], new Options());
			//	var argLine = string.Empty;
			//	var argsList = new List<string>();
			//	do
			//	{
			//		argLine = System.Console.ReadLine();
			//		if (!string.IsNullOrWhiteSpace(argLine))
			//			argsList.Add(argLine);
			//	} while (!string.IsNullOrWhiteSpace(argLine));

			//	args = argsList.ToArray();
			//}

			args = new[]
			{
				"-u http://www.pathofexile.com/item-data/weapon"
			};

			var options = new Options();
			if (!Parser.Default.ParseArguments(args, options))
			{
				System.Console.ReadKey();
				return;
			}

			var client = new HttpClient();
			var responseMessage = client.GetAsync(options.Url).Result;
			var result = responseMessage.Content.ReadAsStreamAsync().Result;

			var doc = new HtmlDocument();
			doc.Load(result);

			var imgTags = doc.DocumentNode.SelectNodes("//img");
			foreach (var imgTag in imgTags)
			{
				imgTag.Remove();
			}

			var itemTables = doc.DocumentNode.SelectNodes("//table[@class='itemDataTable']");
			var items = new List<ItemDef>();

			foreach (var itemTable in itemTables)
			{
				var itemType = itemTable.ParentNode.PreviousSibling.PreviousSibling.InnerText;

				var propSetters = new Dictionary<string, Action<ItemDef, string>>
				{
					{"Name", (def, s) => def.Name = s},
					{"Level", (def, s) => def.Level = s},
					{"Damage", (def, s) => def.Damage = s},
					{"Attacks per second", (def, s) => def.Aps = s},
					{"DPS", (def, s) => def.Dps = s},
					{"Req Str", (def, s) => def.ReqStr = s},
					{"Req Dex", (def, s) => def.ReqDex = s},
					{"Req Int", (def, s) => def.ReqInt = s},
					{"Implicit Mods", (def, s) => def.ImplicitMod = s},
					{"Mod Values", (def, s) => def.ImplicitModValue = s},
				};

				var headers = itemTable.SelectNodes("tr/th/a/text()");
				var actualSetters = headers.Select(header => propSetters[header.OuterHtml]).ToList();

				var rows = itemTable.SelectNodes("tr[@class='even']|tr[@class='odd']");
				foreach (var row in rows)
				{
					var item = new ItemDef();
					item.ItemType = itemType;
					items.Add(item);
					var cells = row.SelectNodes("td/text()");
					if (cells == null)
						continue;

					for (var index = 0; index < cells.Count; index++)
					{
						var cell = cells[index];
						actualSetters[index].Invoke(item, cell.OuterHtml);
					}

					var cells2 = row.NextSibling.NextSibling.SelectNodes("td/text()");
					if (cells2 != null)
					{
						for (var index = 0; index < cells2.Count; index++)
						{
							var cell2 = cells2[index];
							actualSetters[index + cells.Count].Invoke(item, cell2.OuterHtml);
						}
					}

					System.Console.WriteLine(item.Name);
					Thread.Sleep(10);
				}
			}

			var jsonString = JsonConvert.SerializeObject(items);

			using (var writer = File.CreateText(Path.Combine(Environment.CurrentDirectory, "item.json")))
			{
				writer.Write(jsonString);
			}

			System.Console.ReadKey();
		}
	}

	public class ItemDef
	{
		public string Name { get; set; }
		public string Level { get; set; }
		public string Damage { get; set; }
		public string Aps { get; set; }
		public string Dps { get; set; }
		public string ReqStr { get; set; }
		public string ReqDex { get; set; }
		public string ReqInt { get; set; }
		public string ImplicitMod { get; set; }
		public string ImplicitModValue { get; set; }
		public string ItemType { get; set; }
	}

	internal class Options
	{
		[Option('u', "url", Required = true, HelpText = "Url of the page, that contains the table.")]
		public string Url { get; set; }

		//[Option('t', "tablename", Required = true, HelpText = "Id of the table.")]
		//public string TableId { get; set; }

		[ParserState]
		public IParserState LastParserState { get; set; }

		[HelpOption]
		public string GetUsage()
		{
			return HelpText.AutoBuild(this,
				(HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
		}
	}
}
