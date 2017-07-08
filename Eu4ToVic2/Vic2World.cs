﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;

namespace Eu4ToVic2
{
	class Vic2World
	{

		public Eu4Save Eu4Save { get; set; }

		public static readonly string VIC2_DIR = @"C:\Program Files (x86)\Steam\steamapps\common\Victoria 2\";

		public static readonly string OUTPUT = @"output\";

		public ProvinceMapper ProvMapper { get; set; }
		public Mapper V2Mapper { get; set; }
		public List<Vic2Country> Vic2Countries { get; set; }
		public List<Vic2Province> Vic2Provinces { get; set; }
		public PdxSublist CountryEffects { get; set; }
		public PdxSublist ProvinceEffects { get; set; }
		public Dictionary<Ideology, IdeologyModifier> IdeologyModifiers { get; set; }



		/// <summary>
		/// Stores order technology should be granted in each category
		/// </summary>
		public Dictionary<string, List<string>> TechOrder { get; set; }
		public Dictionary<string, Vic2ReligionGroup> ReligiousGroups { get; set; }

		public Vic2World(Eu4Save eu4Save)
		{

			Eu4Save = eu4Save;
			ReligiousGroups = new Dictionary<string, Vic2ReligionGroup>();
			V2Mapper = new Mapper(this);
			ProvMapper = new ProvinceMapper("province_mappings.txt");
			LoadEffects();
			LoadPoliticalParties();
			LoadVicTech();
			LoadVicReligion();
			Console.WriteLine("Constructing Vic2 world...");
			GenerateCountries();
			GenerateProvinces();
			Console.WriteLine("Generating mod...");
			CreateModFolders();
			CreateCountryFiles();
			CreateProvinceFiles();
			CreatePopFiles();
			CreateReligionFile();
			Console.WriteLine("Done!");
		}

		private void CreateReligionFile()
		{
			var data = new PdxSublist();
			foreach (var rel in ReligiousGroups)
			{
				data.AddSublist(rel.Key, rel.Value.GetData());
				
			}
			using (var file = File.CreateText(Path.Combine(OUTPUT, @"common\religion.txt")))
			{
				data.WriteToFile(file);
			}
		}

		private void LoadVicReligion()
		{
			ReligiousGroups = new Dictionary<string, Vic2ReligionGroup>();
			var religions = PdxSublist.ReadFile(Path.Combine(VIC2_DIR, @"common\religion.txt"));
			foreach (var relGroup in religions.Sublists)
			{
				ReligiousGroups[relGroup.Key] = new Vic2ReligionGroup(relGroup.Value);
			}
		}

		private void CreatePopFiles()
		{
			var histDir = Directory.CreateDirectory(Path.Combine(OUTPUT, @"history\pops"));
			var startDir = Directory.CreateDirectory(Path.Combine(OUTPUT, @"history\pops\1836.1.1"));
			var vanillaFiles = Directory.GetFiles(Path.Combine(VIC2_DIR, @"history\pops\1836.1.1"));
			foreach (var file in vanillaFiles)
			{
				using (File.CreateText(Path.Combine(startDir.FullName, Path.GetFileName(file)))) ;
			}
			var pops = new PdxSublist(null);
			foreach (var province in Vic2Provinces)
			{
				var pop = province.GetPopData();
				pop.Parent = pops;
				pops.AddSublist(province.ProvID.ToString(), pop);
			}
			using (var file = File.CreateText(Path.Combine(startDir.FullName, "1836.txt")))
			{
				pops.WriteToFile(file);
			}
		}

		private void CreateProvinceFiles()
		{
			var histDir = Directory.CreateDirectory(Path.Combine(OUTPUT, @"history\provinces"));
			foreach (var province in Vic2Provinces)
			{
				if (!Directory.Exists(Path.Combine(histDir.FullName, province.Subfolder)))
				{
					Directory.CreateDirectory(Path.Combine(histDir.FullName, province.Subfolder));
				}
				//history\provinces
				using (var file = File.CreateText(Path.Combine(histDir.FullName, province.Subfolder, province.FileName)))
				{
					province.GetProvinceData().WriteToFile(file);
				}
			}
		}

		private void CreateModFolders()
		{

			if (Directory.Exists(OUTPUT))
			{
				Directory.Delete(OUTPUT, true);
			}
			Directory.CreateDirectory(OUTPUT);
			Directory.CreateDirectory(Path.Combine(OUTPUT, "common"));
			Directory.CreateDirectory(Path.Combine(OUTPUT, "history"));
			Directory.CreateDirectory(Path.Combine(OUTPUT, "gfx"));
		}

		public string GenerateReligion(string religion)
		{
			var group = Eu4Save.ReligiousGroups.FirstOrDefault(g => g.Value.Religions.Contains(Eu4Save.Religions[religion])).Value;
			if (!ReligiousGroups.ContainsKey(group.Name))
			{
				ReligiousGroups[group.Name] = new Vic2ReligionGroup(group.Name);
			}
			ReligiousGroups[group.Name].AddReligion(religion, Eu4Save);
			return religion;
		}

		private void CreateCountryFiles()
		{
			//common
			var vanillaCtry = PdxSublist.ReadFile(Path.Combine(VIC2_DIR, @"common\countries.txt"));
			var txt = Path.Combine(OUTPUT, @"common\countries.txt");
			using (var txtFile = File.CreateText(txt))
			{
				foreach (var country in Vic2Countries)
				{
					txtFile.WriteLine($"{country.CountryTag} = \"countries/{country.CountryTag}.txt\"");
				}
				txtFile.WriteLine("# From vanilla");
				foreach (var ctry in vanillaCtry.KeyValuePairs)
				{
					var genCtry = Vic2Countries.Find(c => c.CountryTag == ctry.Key);
					if (genCtry == null)
					{
						txtFile.WriteLine($"{ctry.Key} = {ctry.Value}");
					}
				}

			}


			var dir = Directory.CreateDirectory(Path.Combine(OUTPUT, @"common\countries"));
			var histDir = Directory.CreateDirectory(Path.Combine(OUTPUT, @"history\countries"));
			var flagDir = Directory.CreateDirectory(Path.Combine(OUTPUT, @"gfx\flags"));
			foreach (var country in Vic2Countries)
			{
				//common\countries
				using (var file = File.CreateText(Path.Combine(dir.FullName, $"{country.CountryTag}.txt")))
				{
					country.GetCommonCountryFile().WriteToFile(file);
				}
				//history\countries
				using (var file = File.CreateText(Path.Combine(histDir.FullName, $"{country.CountryTag}.txt")))
				{
					country.GetHistoryCountryFile().WriteToFile(file);
				}

				var suffixes = new string[] { "", "_communist", "_fascist", "_monarchy", "_republic" };
				foreach (var suff in suffixes)
				{
					File.Copy($"ENG{suff}.tga", Path.Combine(flagDir.FullName, $"{country.CountryTag}{suff}.tga"));
				}

			}
		}

		private void GenerateProvinces()
		{
			Console.WriteLine("Mapping provinces...");
			Vic2Provinces = new List<Vic2Province>();
			var provs = new Dictionary<int, List<Eu4Province>>();
			ProvMapper.Mappings.Sublists["mappings"].GetAllMatchingSublists("link", (lnk) =>
			{
				var eu4Provs = new List<Eu4Province>();
				lnk.GetAllMatchingKVPs("eu4", (eu4Prov) =>
				{
					var eu4 = int.Parse(eu4Prov);
					if (Eu4Save.Provinces.ContainsKey(eu4))
					{
						eu4Provs.Add(Eu4Save.Provinces[eu4]);
					}
				});
				lnk.GetAllMatchingKVPs("v2", (v2Prov) =>
				{
					var v2 = int.Parse(v2Prov);
					if (provs.ContainsKey(v2))
					{
						provs[v2].AddRange(eu4Provs);
					}
					else {
						provs[v2] = new List<Eu4Province>(eu4Provs);
					}

				});
			});



			foreach (var prov in provs)
			{
				var v2Prov = FindProvinceFile(prov.Key);
				if (v2Prov != null)
				{
					Vic2Provinces.Add(new Vic2Province(prov.Key, v2Prov, this, provs.Count(p => prov.Value.All(p.Value.Contains)), prov.Value));
				}
			}

			Console.WriteLine($"Mapped {Vic2Provinces.Count} provinces.");
		}
		public int GetBestVic2ProvinceMatch(int eu4ProvID)
		{
			return int.Parse(ProvMapper.Mappings.Sublists["mappings"].Sublists.First(s =>
			{
				var found = false;
				s.Value.GetAllMatchingKVPs("eu4", prov =>
				{
					found |= prov == eu4ProvID.ToString();
				});
				return found;
			}).Value.KeyValuePairs["v2"]);
		}

		private PdxSublist FindProvinceFile(int key)
		{
			var provinces = Path.Combine(VIC2_DIR, @"history\provinces");
			var provDir = Directory.GetDirectories(provinces);
			foreach (var dir in provDir)
			{
				var files = Directory.GetFiles(dir);
				var provFile = files.FirstOrDefault(f => Path.GetFileName(f).StartsWith(key + " "));
				if (provFile != default(string))
				{
					return PdxSublist.ReadFile(provFile);
				}
			}
			return null;
		}

		public Vic2Country GetCountry(Eu4Country eu4Country)
		{
			return Vic2Countries.Find(c => c.Eu4Country == eu4Country);
		}
		public void ValueEffect(PdxSublist effects, Action<Dictionary<string, float>> callback, string key, float value)
		{
			if (effects.Sublists.ContainsKey("values") && effects.Sublists["values"].Sublists.ContainsKey(key))
			{
				effects.Sublists["values"].GetAllMatchingSublists(key, (sub) =>
				{
					var average = 0f;
					if (sub.KeyValuePairs.ContainsKey("average"))
					{
						average = float.Parse(sub.KeyValuePairs["average"]);
					}
					var min = float.MinValue;
					if (sub.KeyValuePairs.ContainsKey("minimum"))
					{
						min = float.Parse(sub.KeyValuePairs["minimum"]);
					}
					var max = float.MaxValue;
					if (sub.KeyValuePairs.ContainsKey("maximum"))
					{
						max = float.Parse(sub.KeyValuePairs["maximum"]);
					}

					callback(sub.KeyValuePairs.ToDictionary(effect => effect.Key, effect => Math.Min(max, Math.Max(min, (value - average))) * float.Parse(effect.Value)));
				});

			}
		}
		// do not look in here, it's an ugly mess
		private void LoadVicTech()
		{
			Console.WriteLine("Loading vic2 technologies...");
			var techs = PdxSublist.ReadFile(Path.Combine(VIC2_DIR, @"common\technology.txt"));
			var techTypes = techs.Sublists["folders"];
			TechOrder = new Dictionary<string, List<string>>();
			foreach (var techType in techTypes.Sublists)
			{
				TechOrder.Add(techType.Key, new List<string>());
				var techTypeFile = PdxSublist.ReadFile(Path.Combine(VIC2_DIR, $@"technologies\{techType.Key}.txt"));
				//list instead of dictionary to retain order
				var subTypes = new List<KeyValuePair<string, Queue<string>>>();
				foreach (var tech in techTypeFile.Sublists)
				{
					if (!subTypes.Exists(p => p.Key == tech.Value.KeyValuePairs["area"]))
					{
						subTypes.Add(new KeyValuePair<string, Queue<string>>(tech.Value.KeyValuePairs["area"], new Queue<string>()));
					}
					// a big mess
					subTypes.Find(kv => kv.Key == tech.Value.KeyValuePairs["area"]).Value.Enqueue(tech.Key);
				}
				var subTypesList = subTypes.ConvertAll(st => st.Value);
				while (subTypesList.Count > 0)
				{
					for (var i = 0; i < subTypesList.Count; i++)
					{
						TechOrder[techType.Key].Add(subTypesList[i].Dequeue());
						if (subTypesList[i].Count == 0)
						{
							subTypesList.RemoveAt(i--);
						}

					}

				}
			}


		}

		private void LoadPoliticalParties()
		{
			Console.WriteLine("Loading party ideologies...");
			IdeologyModifiers = new Dictionary<Ideology, IdeologyModifier>();
			var parties = PdxSublist.ReadFile("political_parties.txt");
			foreach (var ideology in (Ideology[])Enum.GetValues(typeof(Ideology)))
			{
				var name = Enum.GetName(typeof(Ideology), ideology);
				if (parties.Sublists.ContainsKey(name))
				{
					var party = new IdeologyModifier();

					foreach (var policy in Policies.policyTypes)
					{
						if (parties.Sublists[name].KeyValuePairs.ContainsKey(policy.Name))
						{
							party.AddModifier(policy, float.Parse(parties.Sublists[name].KeyValuePairs[policy.Name]));
						}
					}
					IdeologyModifiers.Add(ideology, party);
				}
			}
		}

		private void LoadEffects()
		{
			Console.WriteLine("Loading countryEffects.txt...");
			CountryEffects = PdxSublist.ReadFile("countryEffects.txt");
			Console.WriteLine("Loading provinceEffects.txt...");
			ProvinceEffects = PdxSublist.ReadFile("provinceEffects.txt");
		}

		private void GenerateCountries()
		{
			Console.WriteLine("Creating Vic2 countries...");
			Vic2Countries = new List<Vic2Country>();
			foreach (var eu4Country in Eu4Save.Countries.Values)
			{
				var v2Country = new Vic2Country(this, eu4Country);
				Vic2Countries.Add(v2Country);
			}

			Console.WriteLine($"Created {Vic2Countries.Count} Vic2 countries...");
		}

	}

	public static class Policies
	{
		public static Type[] policyTypes = { typeof(economic_policy), typeof(trade_policy), typeof(religious_policy), typeof(war_policy), typeof(citizenship_policy) };
	}
	public enum economic_policy
	{
		planned_economy,
		state_capitalism,
		interventionism,
		laissez_faire
	}

	public enum trade_policy
	{
		free_trade,
		protectionism
	}
	public enum religious_policy
	{
		pro_atheism,
		secularized,
		pluralism,
		moralism
	}

	public enum war_policy
	{
		pacifism, anti_military, pro_military, jingoism
	}
	public enum citizenship_policy
	{
		full_citizenship, limited_citizenship, residency
	}

	public class IdeologyModifier
	{
		public Dictionary<Type, float> Policies { get; set; }
		public IdeologyModifier()
		{
			Policies = new Dictionary<Type, float>();
		}
		internal void AddModifier(Type policy, float value)
		{
			if (!Policies.ContainsKey(policy))
			{
				Policies.Add(policy, 0);
			}
			Policies[policy] += value;

		}

		public static IdeologyModifier operator +(IdeologyModifier a, IdeologyModifier b)
		{
			var newMod = new IdeologyModifier();
			foreach (var pol in a.Policies)
			{
				newMod.AddModifier(pol.Key, pol.Value);
			}
			foreach (var pol in b.Policies)
			{
				newMod.AddModifier(pol.Key, pol.Value);
			}
			return newMod;
		}
		//public int EconomicPolicy { get { return Policies[typeof(economic_policy); } }
		//public int TradePolicy { get; }
		//public int ReligiousPolicy { get;  }
		//public int WarPolicy { get;  }
		//public int CitizenshipPolicy { get;  }

	}
}