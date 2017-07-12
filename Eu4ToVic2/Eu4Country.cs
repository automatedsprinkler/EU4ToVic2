﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Eu4ToVic2
{
	public class Colour
	{
		public byte Red { get; set; }
		public byte Green { get; set; }
		public byte Blue { get; set; }

		public Colour(byte r, byte g, byte b)
		{
			Red = r;
			Green = g;
			Blue = b;
		}


		public Colour(List<string> rgb) 
		{
			
			byte r;
			byte g;
			byte b;
			if(byte.TryParse(rgb[0], out r) && byte.TryParse(rgb[1], out g) && byte.TryParse(rgb[2], out b))
			{
				Red = r;
				Green = g;
				Blue = b;
			} else
			{
				Red = (byte)(float.Parse(rgb[0]) * 255);
				Green = (byte)(float.Parse(rgb[1]) * 255);
				Blue = (byte)(float.Parse(rgb[2]) * 255);
			}
		}

		public Colour(List<string> rgb, byte multiplier): this((byte)(multiplier * float.Parse(rgb[0])), (byte)(multiplier * float.Parse(rgb[1])), (byte)(multiplier * float.Parse(rgb[2])))
		{

		}

	}

	public class Estate
	{
		public static readonly string[] EstateTypes = new string[] { null, "estate_church", "estate_nobles", "estate_burghers", "estate_cossacks", "estate_nomadic_tribes", "estate_dhimmi"};
		public string Type { get; set; }
		public float Loyalty { get; set; }
		public float Influence { get; set; }
		public float Territory { get; set; }

		public Estate(PdxSublist estate)
		{
			Type = estate.GetString("type");
			Loyalty = float.Parse(estate.GetString("loyalty"));
			Influence = float.Parse(estate.GetString("influence"));
			Territory = float.Parse(estate.GetString("territory"));
		}
	}

	public class Eu4Country
	{

		public bool Exists { get; set; }

		public string DisplayNoun { get; set; }
		public string DisplayAdj { get; set; }

		public byte GovernmentRank { get; set; }

		public List<bool> Institutions { get; private set; }
		public string CountryTag { get; set; }
		public string Overlord { get; set; }
		public float LibertyDesire { get; set; }

		public int Capital { get; set; }

		public Colour MapColour { get; set; }

		public string PrimaryCulture { get; set; }
		public List<string> AcceptedCultures { get; set; }
		public string Religion { get; set; }

		public byte AdmTech { get; set; }
		public byte DipTech { get; set; }
		public byte MilTech { get; set; }

		public List<Estate> Estates { get; set; }

		public float PowerProjection { get; set; }

		public DateTime LastElection { get; set; }

		public float Prestige { get; set; }
		public sbyte Stability { get; private set; }
		public float Inflation { get; private set; }


		public int Debt { get; set; }

		public float Absolutism { get; set; }
		public float Legitimacy { get; set; }
		public float RepublicanTradition { get; set; }
		public float Corruption { get; set; }
		public float Mercantilism { get; private set; }

		public Dictionary<string, byte> Ideas { get; set; }

		public string Government { get; private set; }

		public List<string> Flags { get; set; }

		public List<string> Policies { get; set; }
		public bool IsColonialNation { get; private set; }
		public List<int> Opinions { get; set; }
		public Eu4Country(PdxSublist country, Eu4Save save)
		{
			CountryTag = country.Key;
			Opinions = country.GetSublist("opinion_cache").Values.Select(int.Parse).ToList();
			//Console.WriteLine($"Loading {CountryTag}...");
			if (country.KeyValuePairs.ContainsKey("name"))
			{
				DisplayNoun = country.GetString("name").Replace("\"", string.Empty);
			} else { 
				DisplayNoun = save.Localisation[CountryTag];
			}
			if (country.KeyValuePairs.ContainsKey("adjective"))
			{
				DisplayAdj = country.GetString("adjective").Replace("\"", string.Empty);
			}
			else {
				DisplayAdj = save.Localisation[$"{CountryTag}_ADJ"];
			}

			Exists = country.Sublists.ContainsKey("owned_provinces");

			if (country.KeyValuePairs.ContainsKey("overlord"))
			{
				Overlord = country.GetString("overlord").Replace("\"", string.Empty);
			}
			if (country.KeyValuePairs.ContainsKey("liberty_desire"))
			{
				LibertyDesire = float.Parse(country.GetString("liberty_desire"));
			}
			if (country.KeyValuePairs.ContainsKey("colonial_parent"))
			{
				IsColonialNation = true;
			}

			var institutions = country.GetSublist("institutions");
			Institutions = institutions.Values.Select(ins => int.Parse(ins) == 1).ToList();
			Capital = int.Parse(country.GetString("capital"));
			var colours = country.GetSublist("colors");
			var mapColour = colours.GetSublist("map_color");
			MapColour = new Colour(mapColour.Values);

			PrimaryCulture = country.GetString("primary_culture");

			AcceptedCultures = new List<string>();

			country.KeyValuePairs.ForEach("accepted_culture", (value) =>
			{
				AcceptedCultures.Add(value);
			});

			Religion = country.GetString("religion");

			GovernmentRank = byte.Parse(country.GetString("government_rank"));

			var tech = country.GetSublist("technology");
			AdmTech = (byte)tech.GetFloat("adm_tech");
			DipTech = (byte)tech.GetFloat("dip_tech");
			MilTech = (byte)tech.GetFloat("adm_tech");

			Estates = new List<Estate>();
			country.Sublists.ForEach("estate", (est) =>
			{
				Estates.Add(new Estate(est));
			});


			PowerProjection = LoadFloat(country, "current_power_projection");

			LastElection = country.GetDate("last_election");

			Prestige = LoadFloat(country, "prestige");

			Stability = (sbyte)country.GetFloat("stability");
			Inflation = LoadFloat(country, "inflation");
			

			country.GetAllMatchingSublists("loan", (loan) =>
			{
				Debt += (int)loan.GetFloat("amount");
			});

			Absolutism = LoadFloat(country,"absolutism");
			Legitimacy = LoadFloat(country,"legitimacy", 50);
			RepublicanTradition = LoadFloat(country, "republican_tradition", 50);
			Corruption = LoadFloat(country,"corruption");
			Mercantilism = LoadFloat(country,"mercantilism");

			Ideas = new Dictionary<string, byte>();
			var ideas = country.GetSublist("active_idea_groups");
			foreach (var idp in ideas.FloatValues)
			{
				Ideas.Add(idp.Key, (byte)idp.Value.Single());
			}

			Flags = country.GetSublist("flags").KeyValuePairs.Keys.ToList();
			Policies = new List<string>();
			country.Sublists.ForEach("active_policy", (pol) =>
			{
				Policies.Add(pol.GetString("policy"));
			});

			Government = country.GetString("government");
			if (country.Key == "GBR")
			{
			//	Console.WriteLine(Institutions);
			}
		}

		private float LoadFloat(PdxSublist country, string key, float deflt = 0)
		{
			if (!country.FloatValues.ContainsKey(key))
			{
				return deflt;
			}
			return country.GetFloat(key);
		}
	}
}
