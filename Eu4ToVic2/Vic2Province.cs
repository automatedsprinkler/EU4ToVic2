﻿using Eu4Helper;
using PdxFile;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Eu4ToVic2
{
	public class Vic2Province
	{
		public List<Eu4ProvinceBase> Eu4Provinces;

		public string Subfolder { get; set; }
		public string FileName { get; set; }


		public int ProvID { get; set; }
		public Vic2Country Owner { get; set; }
		public List<Vic2Country> Cores { get; set; }

		public string TradeGoods { get; set; }
		public int LifeRating { get; set; }

		public float FortLevel { get; set; }

		public float RailroadLevel { get; set; }

		public HashSet<string> Factories { get; set; }

		public PopPool Pops { get; set; }
		public Vic2World World { get; private set; }
		public int SiblingProvinces { get; private set; }
		public Point MapPosition { get; private set; }

		public Eu4Religion MajorityReligion { get; set; }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="provID">ID of the province</param>
		/// <param name="defaultProvince">The province's default history file</param>
		/// <param name="vic2World">The world object the province exists in</param>
		/// <param name="siblingProvinces">The number of other vic2 provinces that eu4Provinces has been mapped to</param>
		/// <param name="eu4Provinces">List of eu4 provinces mapped to this province</param>
		public Vic2Province(int provID, PdxSublist defaultProvince, Vic2World vic2World, int siblingProvinces, List<Eu4ProvinceBase> eu4Provinces)
		{
			DoSetup(provID, defaultProvince, vic2World, siblingProvinces, eu4Provinces);
		}

		private void DoSetup(int provID, PdxSublist defaultProvince, Vic2World vic2World, int siblingProvinces, List<Eu4ProvinceBase> eu4Provinces)
		{
			World = vic2World;
			Subfolder = Path.GetFileName(Path.GetDirectoryName(defaultProvince.Key));
			FileName = Path.GetFileName(defaultProvince.Key);
			Eu4Provinces = eu4Provinces;
			ProvID = provID;
			TradeGoods = defaultProvince.GetString("trade_goods");
			LifeRating = (int)defaultProvince.GetFloat("life_rating");
			Pops = new PopPool(vic2World, this);
			Factories = new HashSet<string>();
			SiblingProvinces = siblingProvinces;
			if(ProvID == 222)
			{
				Console.WriteLine();
			}
			var mapPos = vic2World.Vic2ProvPositions[ProvID.ToString()];
			var mapX = mapPos.Sum(p => p.X) / mapPos.Count;
			var mapY = mapPos.Sum(p => p.Y) / mapPos.Count;
			MapPosition = new Point(mapX, mapY);
			if (eu4Provinces.Count > 0)
			{
				// most common owner
				Owner = vic2World.GetCountry(eu4Provinces.GroupBy(p => p.Owner).OrderByDescending(grp => grp.Count())
		  .Select(grp => grp.Key).First());
				if (Owner?.Eu4Country?.IsColonialNation ?? false)
				{
					Owner = vic2World.GetCountry(Owner.Eu4Country.Overlord);
				}
				if (Owner != null)
				{
					Owner.NumProvinces++;
				}
				// all countries that have full cores in any of the eu4 counterparts gets cores here
				Cores = eu4Provinces.Where(p => p.IsState).SelectMany(p => p.Cores).Select(c => vic2World.GetCountry(c)).Distinct().ToList();
				//Cores = new List<Vic2Country>();
				var r = eu4Provinces.GroupBy(p => p.Religion).OrderByDescending(g => g.Sum(p => p.Development)).First().First().Religion;
				if (r != "no_religion")
				{
					MajorityReligion = World.Eu4Save.Religions[r];
				}
				
				FortLevel = eu4Provinces.Any(p => p.FortLevel > 6) ? 1 : 0;

				
			}
			else
			{
				Pops.ReadData(vic2World.PopData.Sublists[provID.ToString()]);
			}


		}

		public void PostInitialise()
		{
			if (Eu4Provinces.Count > 0)
			{
				CalcEffects(World, SiblingProvinces, Eu4Provinces);

				var largeCultures = Pops.GetLargeCultures(0.4f);
				// TODO: dynamically create countries that don't have a primary nation in vic2
				foreach (var cul in largeCultures)
				{
					World.CultureNations.KeyValuePairs.ForEach(cul, tag =>
					{
						Cores.Add(World.Vic2Countries.Find(c => c.CountryTag == tag) ?? new Vic2Country(World, tag, World.Cultures[cul]));
					});
					// add neoculture as accepted culture
					if (World.V2Mapper.NeoCultures.ContainsKey(cul) && World.V2Mapper.NeoCultures[cul] == Owner?.PrimaryCulture)
					{
						if (!Owner.AcceptedCultures.Contains(cul))
						{
							Owner.AcceptedCultures.Add(cul);
						}
					}
					if (World.Cultures[cul].PrimaryNation == null)
					{
						if (World.Cultures[cul].Group.Union == null)
						{


						}
					}
					else {
						Cores.Add(World.Cultures[cul].PrimaryNation);
					}
					if (World.Cultures[cul].Group.Union != null)
					{
						Cores.Add(World.Cultures[cul].Group.Union);
					}
				}
			}
			//effects on owner
			if (Owner != null)
			{
				IterateEffects(World, World.CountryEffects.Sublists["province"], 1, Eu4Provinces, (effects, fromProvince) =>
				{
					Owner.FromProvinceEffect(effects);
				});
			}
		}

		public PdxSublist GetProvinceData()
		{
			var file = new PdxSublist(null);
			if (Owner != null)
			{
				file.AddValue("owner", Owner.CountryTag);
				file.AddValue("controller", Owner.CountryTag);
			}
			if (Cores != null)
			{
				foreach (var core in Cores)
				{
					file.AddValue("add_core", core.CountryTag);
				}
			}
			file.AddValue("trade_goods", TradeGoods);
			file.AddValue("fort", FortLevel.ToString());
			file.AddValue("railroad", RailroadLevel.ToString());
			foreach (var factory in Factories)
			{
				var factoryData = new PdxSublist();

				factoryData.AddValue("level", "1");
				factoryData.AddValue("building", factory);
				factoryData.AddValue("upgrade", "yes");

				file.AddSublist("state_building", factoryData);
			}


			return file;
		}

		public PdxSublist GetPopData()
		{
			//if(Eu4Provinces.Count == 0)
			//{
			//	return null;
			//}
			return Pops.GetData(this);
		}

		private void CalcEffects(Vic2World vic2World, int siblingProvinces, List<Eu4ProvinceBase> eu4Provinces)
		{
			var factories = new Dictionary<string, float>();
			var baseFactories = 0f;
			//Factories = new HashSet<string>();
			
			IterateEffects(vic2World, vic2World.ProvinceEffects, siblingProvinces, eu4Provinces, (effects, fromProvince) =>
			{
				CalcPopEffects(effects, fromProvince);
				baseFactories += CalcFactoryEffects(effects, fromProvince, factories);
				if (effects.ContainsKey("railroad")) {
					RailroadLevel += effects["railroad"];
				}
				if (effects.ContainsKey("fort"))
				{
					RailroadLevel += effects["fort"];
				}
			});
			IterateEffects(vic2World, vic2World.ProvinceEffects, siblingProvinces, eu4Provinces, (effects, fromProvince) =>
			{
				CalcRelativePopEffects(effects, fromProvince);
			});
			FinaliseFactoryEffects(factories, baseFactories);

			//on country
			
		}

		private void FinaliseFactoryEffects(Dictionary<string, float> factories, float baseFactories)
		{
			if (ProvID == 300)
			{
				Console.WriteLine();
			}
			var totalFactories = new Dictionary<string, float>();
			
			foreach (var factory in World.Factories.Sublists["priority"].Values)
			{
				var val = factories.ContainsKey(factory) ? factories[factory] : 0;
				totalFactories[factory] = val + baseFactories;
			}
			// in order of highest score to lowest score, for equal scores it's ordered by the order in the priority section of factories.txt
			var orderedFactories = totalFactories.OrderByDescending(f => f.Value).ThenBy(f => World.Factories.Sublists["priority"].Values.IndexOf(f.Key));
			foreach (var factory in orderedFactories)
			{
				if(factory.Value < 1)
				{
					break;
				}

				if (Factories.Add(factory.Key))
				{
					FinaliseFactoryEffects(factories, baseFactories - 1);
					break;
				}
			}
		}

		private float CalcFactoryEffects(Dictionary<string, float> effects, Eu4ProvinceBase fromProvince, Dictionary<string, float> factories)
		{
			foreach (var factory in World.Factories.Sublists["priority"].Values)
			{
				if (effects.ContainsKey(factory))
				{
					if (!factories.ContainsKey(factory))
					{
						factories[factory] = 0;
					}
					factories[factory] += effects[factory];
				}
			}
			if (effects.ContainsKey("factory"))
			{
				return effects["factory"];
			}
			return 0;
		}

		private void CalcRelativePopEffects(Dictionary<string, float> effects, Eu4ProvinceBase fromProvince)
		{
			foreach (PopType popType in Enum.GetValues(typeof(PopType)))
			{
				var name = Enum.GetName(typeof(PopType), popType);
				if (effects.ContainsKey("relative_" + name))
				{
					Pops.IncreaseJob(popType, effects["relative_" + name]);
				}
			}
		}

		private void CalcPopEffects(Dictionary<string, float> effects, Eu4ProvinceBase fromProvince)
		{
			foreach (PopType popType in Enum.GetValues(typeof(PopType)))
			{
				var name = Enum.GetName(typeof(PopType), popType);
				if (effects.ContainsKey(name))
				{
					Pops.AddPop(fromProvince, popType, (int)effects[name]);
				}
			}
		}

		private void IterateEffects(Vic2World vic2World, PdxSublist provEffects, int siblingProvinces, List<Eu4ProvinceBase> eu4Provinces, Action<Dictionary<string, float>, Eu4ProvinceBase> callback)
		{


			foreach (var prov in eu4Provinces)
			{
				Action<Dictionary<string, float>> newCallback = (Dictionary<string, float> effects) =>
				{
					callback(effects, prov);
				};
				// development
				vic2World.ValueEffect(provEffects, newCallback, "base_tax", prov.BaseTax / (float)siblingProvinces);
				vic2World.ValueEffect(provEffects, newCallback, "base_production", prov.BaseProduction / (float)siblingProvinces);
				vic2World.ValueEffect(provEffects, newCallback, "base_manpower", prov.BaseManpower / (float)siblingProvinces);
				// total dev
				vic2World.ValueEffect(provEffects, newCallback, "development", (prov.BaseTax + prov.BaseProduction + prov.BaseManpower) / (float)siblingProvinces);

				// estates
				if (prov.Estate != null && provEffects.Sublists["estate"].Sublists.ContainsKey(prov.Estate))
				{
					newCallback(provEffects.Sublists["estate"].Sublists[prov.Estate].FloatValues.ToDictionary(effect => effect.Key, effect => effect.Value.Sum() / eu4Provinces.Count));
				}


				// province flags
				if (provEffects.Sublists.ContainsKey("province_flags") && prov.Flags != null)
				{
					foreach (var flag in prov.Flags)
					{
						if (provEffects.Sublists["province_flags"].Sublists.ContainsKey(flag))
						{
							newCallback(provEffects.Sublists["province_flags"].Sublists[flag].FloatValues.ToDictionary(effect => effect.Key, effect => effect.Value.Sum() / eu4Provinces.Count));
						}
					}
				}
			
				// buildings
				if (provEffects.Sublists.ContainsKey("buildings"))
				{
					foreach (var build in prov.Buildings)
					{
						if (provEffects.Sublists["buildings"].Sublists.ContainsKey(build))
						{
							newCallback(provEffects.Sublists["buildings"].Sublists[build].FloatValues.ToDictionary(effect => effect.Key, effect => effect.Value.Sum() / eu4Provinces.Count));
						}
					}

				}
				// from owner country 
				if (prov.Owner != null && provEffects.Sublists.ContainsKey("owner"))
				{
					Vic2Country.IterateCountryEffects(vic2World, prov.Owner, provEffects.Sublists["owner"], (effects) => { callback(effects, null); });
				}

			}

			
			
		}
	}

	public class PopPool
	{
		public Vic2World World { get; set; }
		private Dictionary<string, Pop> pops;
		//private Dictionary<Eu4Province, ValueSet<string>> culture;
		//private Dictionary<Eu4Province, ValueSet<string>> religion;
		public List<Pop> Pops
		{
			get
			{
				return pops.Values.ToList();
			}
		}

		public Vic2Province province { get; private set; }

		public PopPool(Vic2World world, Vic2Province province)
		{
			World = world;
			pops = new Dictionary<string, Pop>();
			this.province = province;
			//culture = new Dictionary<Eu4Province, ValueSet<string>>();
			//religion = new Dictionary<Eu4Province, ValueSet<string>>();
			//foreach (var prov in eu4Provinces)
			//{
			//	culture[prov] = HistoryEffect(prov.CulturalHistory);
			//	religion[prov] = HistoryEffect(prov.ReligiousHistory);
			//}
		}
		/// <summary>
		/// Gets a list of all cultures in the pool that have at least the threshhold as a proportion of the total population
		/// </summary>
		/// <param name="threshhold"></param>
		/// <returns></returns>
		public List<string> GetLargeCultures(float threshhold)
		{
			var cultures = new Dictionary<string, int>();
			foreach (var pop in pops)
			{
				if (!cultures.ContainsKey(pop.Value.Culture))
				{
					cultures[pop.Value.Culture] = 0;
				}
				cultures[pop.Value.Culture] += pop.Value.Size;
			}
			var total = Pops.Sum(p => p.Size);
			return cultures.Where(c => c.Value / (float)total >= threshhold).Select(c => c.Key).ToList();
		}

		// calculate what proportions of the population should be what demographic based on the history of the province
		private ValueSet<string> HistoryEffect(Dictionary<DateTime, string> history)
		{
			var orderedCulturalHistory = history.OrderBy(h => h.Key.Ticks);
			var set = new ValueSet<string>(1f, orderedCulturalHistory.First().Value);

			KeyValuePair<DateTime, string>? lastEntry = null;
			foreach (var entry in orderedCulturalHistory)
			{
				if (lastEntry.HasValue)
				{
					var since = lastEntry.Value.Key - entry.Key;
					// 100 years -> +50%
					set[lastEntry.Value.Value] += (since.Days * 1.369863013698630136986301369863e-5);

				}
				// new culture is set to 50%
				set[entry.Value] = 0.5f;
				lastEntry = entry;
			}
			return set;
		}
		class DemographicEvent
		{
			public string Culture { get; set; }
			public string Religion { get; set; }
			public DemographicEvent()
			{
			}

			public DemographicEvent SetReligion(string religion)
			{
				Religion = religion;
				return this;
			}
			public DemographicEvent SetCulture(string culture)
			{
				Culture = culture;
				return this;
			}
		}
		public void AddPop(Eu4ProvinceBase fromProvince, PopType type, int quantity)
		{
			var history = new Dictionary<DateTime, DemographicEvent>();
			fromProvince.CulturalHistory.ToList().ForEach(ce => history.Add(ce.Key, new DemographicEvent().SetCulture(World.V2Mapper.GetV2Culture(ce.Value, fromProvince, province))));
			fromProvince.ReligiousHistory.ToList().ForEach(re =>
			{
				if (history.ContainsKey(re.Key))
				{
					history[re.Key].SetReligion(World.V2Mapper.GetV2Religion(re.Value));
				}
				else
				{
					history[re.Key] = new DemographicEvent().SetReligion(World.V2Mapper.GetV2Religion(re.Value));
				}
			});
			if (history.Count == 0)
			{
				history[new DateTime(1444, 11, 11)] = new DemographicEvent().SetCulture(World.V2Mapper.GetV2Culture(fromProvince.Culture, fromProvince, province)).SetReligion(World.V2Mapper.GetV2Religion(fromProvince.Religion));
			}
			var orderedHistory = history.OrderBy(he => he.Key.Ticks);

			var popsList = new List<Pop>();
			popsList.Add(new Pop(type, quantity, orderedHistory.First().Value.Culture ?? fromProvince.Culture, orderedHistory.First().Value.Religion ?? fromProvince.Religion));
			KeyValuePair<DateTime, DemographicEvent> lastEntry = orderedHistory.First();
			//if (fromProvince.ProvinceID == 233)
			//{
			//	Console.WriteLine("Cornwall!");
			//}
			//bool firstTime = true;
			var majorityReligion = lastEntry.Value.Religion ?? fromProvince.Religion;
			var majorityCulture = lastEntry.Value.Culture ?? fromProvince.Culture;
			foreach (var entry in orderedHistory)
			{

				var since = entry.Key - lastEntry.Key;
				// 200 years -> +50%
				//set[lastEntry.Value.Value] += (since.Days * 1.369863013698630136986301369863e-5); quantity *
				MergePops(popsList, SplitPops((int)(quantity * Math.Min(1, since.Days * 6.8493150684931506849315068493151e-6)), popsList, c => majorityCulture, r => majorityReligion));
				

					if (entry.Value.Religion == null)
				{
					if (entry.Value.Culture == null)
					{
						// something is probably broken here
						Console.WriteLine($"Warning: Province {fromProvince.ProvinceID} has an invalid history entry for {entry.Key.ToShortDateString()}");

					}
					else
					{
						majorityCulture = entry.Value.Culture;
						// flip 50% of population to new culture. only true faith will convert culture as in eu4
						MergePops(popsList, SplitPops(1 + quantity / 2, popsList.Where(p => p.Religion == majorityReligion).ToList(), c => entry.Value.Culture, r => r));
					}
				}
				else
				{
					majorityReligion = entry.Value.Religion;
					if (entry.Value.Culture == null)
					{
						// flip 50% of population to new religion
						MergePops(popsList, SplitPops(1 + quantity / 2, popsList, c => c, r => entry.Value.Religion));
					}
					else
					{
						majorityCulture = entry.Value.Culture;
						// flip 50% to new religion + culture
						MergePops(popsList, SplitPops(1 + quantity / 2, popsList, c => entry.Value.Culture, r => entry.Value.Religion));
					}
				}

			}
			var now = PdxSublist.ParseDate(World.StartDate);
			var finalSince = now - lastEntry.Key;
			// 200 years -> +50%
			MergePops(popsList, SplitPops((int)(quantity * Math.Min(1, finalSince.Days * 6.8493150684931506849315068493151e-6)), popsList, c => majorityCulture, r => majorityReligion));
			foreach (var pop in popsList)
			{
				AddPop(pop);
			}
		}
		/// <summary>
		/// Merges all pops in listB into listA
		/// </summary>
		/// <param name="listA"></param>
		/// <param name="listB"></param>
		private void MergePops(List<Pop> listA, List<Pop> listB)
		{
			foreach (var pop in listB)
			{
				var aPop = listA.Find(p => p.EquivalentTo(pop));
				if (aPop == null)
				{
					listA.Add(pop);
				}
				else
				{
					aPop.Size += pop.Size;
				}
			}

			listA.RemoveAll(p => p.Size <= 0);
		}

		/// <summary>
		/// Converts of number of people who follow a religion to a new religion and culture, evenly across all pops of the old religion
		/// </summary>
		/// <param name="oldReligion"></param>
		/// <param name="total"></param>
		/// <param name="culture"></param>
		/// <param name="religion"></param>
		private void SplitReligion(string oldReligion, int total, string culture, string religion)
		{
			SplitPops(total, pops.Values.Where(p => p.Religion == oldReligion).ToList(), c => culture, r => religion);
		}
		/// <summary>
		/// Converts a number of people to a new religion and culture, evenly across all pops
		/// </summary>
		/// <param name="total"></param>
		/// <param name="culture"></param>
		/// <param name="religion"></param>
		private void SplitAll(int total, string culture, string religion)
		{
			SplitPops(total, pops.Values.ToList(), c => culture, r => religion).ForEach(AddPop);
		}
		private static List<Pop> SplitPops(int total, List<Pop> popsList, Func<string, string> culture, Func<string, string> religion)
		{
			var eachSplit = total / (popsList.Count == 0 ? 1 : popsList.Count);
			var splitSoFar = 0;
			var newPops = new List<Pop>();
			for (var i = 0; i < popsList.Count; i++)
			{
				var pop = popsList[i];
				//split off of this pop
				var np = pop.Split(eachSplit, culture(pop.Culture), religion(pop.Religion));
				// determine how many have been split off
				splitSoFar += np.Size;
				// determine how many pops are left to split
				var popsLeft = (popsList.Count - i - 1);

				// determine how much each remaining pop must be split
				if (popsLeft > 0)
				{
					eachSplit = (total - splitSoFar) / popsLeft;
				}
				// prepare new pop to be added
				newPops.Add(np);
			}
			return newPops;
		}
		private void AddPop(Pop pop)
		{
			AddPop(pop.Type, pop.Size, pop.Culture, pop.Religion);
		}
		private void AddPop(PopType type, int quantity, string culture, string religion)
		{
			var key = GetKey(type, culture, religion);
			if (pops.ContainsKey(key))
			{
				pops[key].Add(quantity);
			}
			else
			{
				pops.Add(key, new Pop(type, quantity, culture, religion));
			}
		}

		public void SetCultureRatio(Dictionary<string, float> cultures)
		{
			// TODO
		}
		private string GetKey(PopType type, string culture, string religion)
		{
			return $"{type}/{culture}/{religion}";
		}

		internal PdxSublist GetData(Vic2Province province)
		{
			var data = new PdxSublist(null, province.ProvID.ToString());
			foreach (var pop in Pops)
			{
				var job = pop.Job;
				if(province.Factories.Count == 0 && (job == "craftsmen" || job == "clerks"))
				{
					job = "artisans";
				}
				var popData = new PdxSublist(data, job);
				popData.AddValue("culture", pop.Culture);
				popData.AddValue("religion", pop.Religion);
				popData.AddValue("size", pop.Size.ToString());
				data.AddSublist(pop.Job, popData);
			}
			return data;
		}

		public void IncreaseJob(PopType popType, float relativeAmount)
		{
			var pops = SplitPops((int)(Pops.Sum(p => p.Size) * relativeAmount), Pops, c => c, r => r);
			pops.ForEach(AddPop);
		}

		public void ReadData(PdxSublist data)
		{
			var rgx = new Regex(@"\d+$");
			foreach (var pop in data.Sublists)
			{
				var key = rgx.Replace(pop.Key, string.Empty);
				AddPop((PopType)Enum.Parse(typeof(PopType), key), (int)pop.Value.GetFloat("size"), pop.Value.KeyValuePairs["culture"], pop.Value.KeyValuePairs["religion"]);
			}
		}
	}
	/// <summary>
	/// A Dictionary of T, float that has a constant total value. Any time a value is modified it will adjust other values to make sure the sum is constant.
	///
	/// Values cannot be negative.
	/// </summary>
	internal class ValueSet<T> : IEnumerable<KeyValuePair<T, double>>
	{
		double total;
		Dictionary<T, double> values;
		public ValueSet(double total, T firstKey)
		{
			this.total = total;
			values = new Dictionary<T, double>();
			values[firstKey] = total;
		}

		public double this[T key]
		{
			get
			{
				return values[key];
			}
			set
			{
				if (!values.ContainsKey(key))
				{
					values[key] = 0;
				}
				if (values.Count == 1)
				{
					values[key] = total;
					return;
				}
				// works out how much it has changed - cannot go above total value
				var change = Math.Min(value, total) - values[key];
				// all others will decrease by the amount it is change by, shared between them
				var eachChange = -change / (values.Count - 1);
				foreach (var otherKey in values.Keys.ToList())
				{
					values[otherKey] += eachChange;
				}
				values[key] += change;
			}
		}

		public IEnumerator<KeyValuePair<T, double>> GetEnumerator()
		{
			foreach (var val in values)
			{
				yield return val;
			}

		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}

	public enum PopType
	{
		aristocrats, clergymen, artisans, soldiers, labourers, farmers, slaves, capitalists, bureaucrats, craftsmen, officers
	}
	public class Pop
	{

		public PopType Type { get; set; }
		public string Job
		{
			get
			{
				return Enum.GetName(typeof(PopType), Type);
			}
		}
		public int Size { get; set; }
		public string Culture { get; set; }
		public string Religion { get; set; }


		public Pop(PopType type, int size, string culture, string religion)
		{
			Type = type;
			Size = size;
			Culture = culture;
			Religion = religion;
		}
		public void Add(int quantity)
		{
			Size += quantity;
		}

		/// <summary>
		/// Splits off some of this pop into a new pop with a different culture and religion
		/// </summary>
		/// <param name="quantity"></param>
		/// <param name="culture"></param>
		/// <param name="religion"></param>
		/// <returns></returns>
		public Pop Split(int quantity, string culture, string religion)
		{
			var newPop = new Pop(Type, Math.Min(quantity, Size), culture, religion);
			Size = Math.Max(0, Size - quantity);
			return newPop;
		}

		internal bool EquivalentTo(Pop pop)
		{
			return pop.Type == Type && pop.Religion == Religion && pop.Culture == Culture;
		}


	}

	public class Vic2State
	{
		public List<Factory> Factories { get; set; }
	}

	public class Factory
	{
		public string Name { get; set; }
		public int Level { get; set; }
	}
}
