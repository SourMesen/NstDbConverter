using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using System.IO.Compression;

namespace NstDbConverter
{
	public partial class Form1 : Form
	{
		HashSet<string> reliableCrcs = new HashSet<string>();

		string GetNodeAttribute(XmlNode parent, string nodeName, string attribute)
		{
			if(parent.SelectSingleNode(nodeName) != null) {
				XmlNode node = parent.SelectSingleNode(nodeName);
				if(node.Attributes[attribute] != null) {
					return node.Attributes[attribute].Value.ToString();
				}
			}
			return "";
		}

		public Form1()
		{
			InitializeComponent();

			Dictionary<string, List<string>> entries = new Dictionary<string, List<string>>();

			XmlDocument doc = new XmlDocument();
			doc.Load("GoodnesDb.xml");
			string lastCrc = "";
			foreach(XmlNode node in doc.SelectNodes("database/game/cartridge")) {
				ProcessNode(entries, node, false, false);
			}

			doc = new XmlDocument();
			doc.Load("NesCartDB.xml");
			lastCrc = "";
			foreach(XmlNode node in doc.SelectNodes("database/game/cartridge")) {
				lastCrc = ProcessNode(entries, node, false, true, lastCrc);
			}

			doc = new XmlDocument();
			doc.Load("NstDatabase.xml");
			foreach(XmlNode node in doc.SelectNodes("database/game/cartridge")) {
				ProcessNode(entries, node, false, true);
			}
			foreach(XmlNode node in doc.SelectNodes("database/game/arcade")) {
				ProcessNode(entries, node, false, true);
			}

			doc = new XmlDocument();
			doc.Load("MesenCartDB.xml");
			foreach(XmlNode node in doc.SelectNodes("database/game/cartridge")) {
				ProcessNode(entries, node, true, true);
			}
			foreach(XmlNode node in doc.SelectNodes("database/game/arcade")) {
				ProcessNode(entries, node, true, true);
			}

			List<string> lines = new List<string>();
			foreach(KeyValuePair<string, List<string>> kvp in entries) {
				lines.Add(string.Join(",", kvp.Value));
			}
			lines.Sort();

			string header = 
@"###################################################################################
#
# Mesen Game Database
#
# Automatically generated database based on Nestopia's DB and NesCartDB
#
# Generated on " + DateTime.Now.ToString("yyyy-MM-dd") + @" using:
#     -NesCartDB (dated 2017-08-21)
#     -Nestopia UE's latest DB (dated 2015-10-22) 
# 
# Fields: CRC, System, Board, PCB, Chip, Mapper, PrgRomSize, ChrRomSize, ChrRamSize, WorkRamSize, SaveRamSize, Battery, Mirroring, Controller Type, Bus Conflicts, SubMapper, VsSystemType, PpuModel
#
###################################################################################";

			lines.Insert(0, header);

			try {
				File.Delete("C:\\Code\\Mesen\\Libretro\\MesenDB.inc");
				File.Delete("MesenDB.zip");
			} catch { }

			string fileContent = string.Join("\n", lines).Replace("\r\n", "\n").Replace("\n\r", "\n");

			Directory.CreateDirectory("ZipFile");
			File.WriteAllText("MesenDB.txt", fileContent);

			StringBuilder incContent = new StringBuilder();
			using(FileStream stream = File.OpenRead("MesenDB.txt")) {
				byte[] byteArray = new byte[stream.Length];
				if(stream.Position < stream.Length) {
					stream.Read(byteArray, 0, (int)stream.Length);
				}

				incContent.Append("const unsigned char MesenDatabase[" + stream.Length.ToString() + "] = {");
				incContent.AppendLine();
				for(int i = 0; i < byteArray.Length; i++) {
					byte b = byteArray[i];
					if(b < 10) {
						incContent.Append(b.ToString() + ",");
					} else {
						incContent.Append("0x" + b.ToString("X") + ",");
					}
					if(b == '\n') {
						incContent.AppendLine();
					}
				}
				incContent.AppendLine();
				incContent.Append("};");
			}

			File.WriteAllText("C:\\Code\\Mesen\\Libretro\\MesenDB.inc", incContent.ToString());

			File.WriteAllText("C:\\Code\\Mesen\\GUI.NET\\Dependencies\\MesenDB.txt", fileContent);
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			this.Close();
		}

		private string ProcessNode(Dictionary<string,List<string>> entries, XmlNode node, bool forceOverride, bool reliableDb, string lastCrc = "")
		{
			List<string> entry;
			string crc = node.Attributes["crc"] != null ? node.Attributes["crc"].Value.ToString() : "";
			if(crc == lastCrc) {
				return crc;
			}

			string inputType = string.Empty;
			if(node.PreviousSibling != null && node.PreviousSibling.LocalName == "peripherals") {
				string device = GetNodeAttribute(node.PreviousSibling, "device", "type");
				switch(device) {
					case "zapper": inputType = "Zapper"; break;
					case "fourplayer": inputType = "FourPlayer"; break;
					case "rob": inputType = "Rob"; break;
					case "familyfunfitness": inputType = "FamilyFunFitness"; break;
					case "suborkeyboard": inputType = "SuborKeyboard"; break;
					case "partytap": inputType = "PartyTap"; break;
					case "powerpad": inputType = "PowerPad"; break;
					case "powerglove": inputType = "PowerGlove"; break;
					case "turbofile": inputType = "TurboFile"; break;
					case "konamihypershot": inputType = "KonamiHypershot"; break;
					case "familytrainer": inputType = "FamilyTrainer"; break;
					case "arkanoid": inputType = "Arkanoid"; break;
					case "topriderbike": inputType = "TopRiderBike"; break;
					case "barcodeworld": inputType = "BarCodeWorld"; break;
					case "familykeyboard": inputType = "FamilyKeyboard"; break;
					case "mahjong": inputType = "Mahjong"; break;
					case "3dglasses": inputType = "3dGlasses"; break;
					case "miraclepiano": inputType = "MiraclePiano"; break;
					case "pokkunmoguraa": inputType = "PokkunMoguraa"; break;
					case "excitingboxing": inputType = "ExcitingBoxing"; break;
					case "bandaihypershot": inputType = "BandaiHypershot"; break;
					case "crazyclimber": inputType = "CrazyClimber"; break;
					case "battlebox": inputType = "BattleBox"; break;
					case "aladdin": inputType = "Aladdin"; break;

					case "oekakids":
					case "oekakidstablet": inputType = "OekaKidsTablet"; break;

					case "pachinko": inputType = "Pachinko"; break;
					case "racermate": inputType = "Racermate"; break;
					case "uforce": inputType = "UForce"; break;

					case "vsswapped": inputType = "VsSwapped"; break;
					case "vsswapAB": inputType = "VsSwapAB"; break;

					default:
						inputType = device;
						break;
				}
			}

			if(!string.IsNullOrWhiteSpace(crc)) {
				string vsSystemType = "";

				string system = node.Attributes["system"] != null ? node.Attributes["system"].Value.ToString() : "";
				switch(system) {
					case "NES-NTSC": system = "NesNtsc"; break;
					case "NES-PAL":
					case "NES-PAL-A":
					case "NES-PAL-B": system = "NesPal"; break;
					case "Famicom": system = "Famicom"; break;
					case "VS-Unisystem": system = "VsSystem"; break;
					case "VS-Dualsystem":
						vsSystemType = "DualSystem";
						system = "VsSystem";
						break;
					case "Dendy": system = "Dendy"; break;
					case "Playchoice-10": system = "Playchoice"; break;
					default: break;
				}

				string ppuModel = node.Attributes["ppu"] != null ? node.Attributes["ppu"].Value.ToString() : "";
				if(vsSystemType == "") {
					vsSystemType = node.Attributes["protection"] != null ? node.Attributes["protection"].Value.ToString() : "";
				}

				string vsInputType = node.Attributes["input"] != null ? node.Attributes["input"].Value.ToString() : "";

				XmlNode board = node.SelectSingleNode("board");

				if(board != null) {
					string boardType = board.Attributes["type"] != null ? board.Attributes["type"].Value.ToString() : "";
					string busConflicts = "";
					if(board.Attributes["busconflicts"]?.Value != null) {
						busConflicts = board.Attributes["busconflicts"].Value == "yes" ? "Y" : "N";
					}

					string subMapper = "";
					if(board.Attributes["submapper"]?.Value != null) {
						subMapper = board.Attributes["submapper"].Value;
					}

					bool fourScreenMirroring = board.Attributes["fourscreens"]?.Value == "true";
					string pcb = board.Attributes["pcb"] != null ? board.Attributes["pcb"].Value.ToString() : "";
					string mapperID = board.Attributes["mapper"] != null ? board.Attributes["mapper"].Value.ToString() : "";

					int prgRomSize = 0;
					foreach(XmlNode prg in board.SelectNodes("prg")) {
						prgRomSize += int.Parse(prg.Attributes["size"].Value.Replace("k", ""));
					}

					int chrRomSize = 0;
					foreach(XmlNode chr in board.SelectNodes("chr")) {
						chrRomSize += int.Parse(chr.Attributes["size"].Value.Replace("k", ""));
					}

					int chrRamSize = 0;
					foreach(XmlNode vram in board.SelectNodes("vram")) {
						chrRamSize += int.Parse(vram.Attributes["size"].Value.Replace("k", ""));
					}

					if(chrRamSize == 4 && chrRomSize > 0) {
						fourScreenMirroring = true;
						chrRamSize = 0;
					}

					int wramSize = 0;
					int sramSize = 0;
					foreach(XmlNode wram in board.SelectNodes("wram")) {
						if(wram.Attributes["battery"] != null && wram.Attributes["battery"].Value == "1") {
							sramSize += int.Parse(wram.Attributes["size"].Value.Replace("k", ""));
						} else {
							wramSize += int.Parse(wram.Attributes["size"].Value.Replace("k", ""));
						}
					}

					string chip = "";
					var chipNodes = board.SelectNodes("chip");
					if(chipNodes.Count > 0) {
						for(int i = 0; i < chipNodes.Count; i++) {
							var chipNode = chipNodes[i];
							if(chipNode.Attributes["type"] != null) {
								string type = chipNode.Attributes["type"].Value.Replace("<", "").Replace(">", "");
								if(!type.StartsWith("74") && !type.StartsWith("24C") && !type.StartsWith("PAL") && !type.StartsWith("MM1")) {
									chip = type;
									break;
								}
							}
						}
					}					

					string workRamBattery = GetNodeAttribute(board, "wram", "battery");
					string chipBattery = GetNodeAttribute(board, "chip", "battery");
					string battery = "0";

					if(workRamBattery == "1" || chipBattery == "1" || sramSize > 0) {
						battery = "1";
					}

					string mirrorH = GetNodeAttribute(board, "pad", "h");
					string mirrorV = GetNodeAttribute(board, "pad", "v");
					string mirroring = "";
					if(mirrorH == "1") {
						mirroring = "v";
					} else if(mirrorV == "1") {
						mirroring = "h";
					}
					if(mirrorH == "0" && mirrorV == "0") {
						mirroring = "a";
					}

					if(fourScreenMirroring) {
						mirroring = "4";
					}

					entry = new List<string>() { crc, system, boardType, pcb, chip, mapperID, prgRomSize.ToString(), chrRomSize == 0 ? "" : chrRomSize.ToString(), chrRamSize == 0 ? "" : chrRamSize.ToString(), wramSize.ToString(), sramSize.ToString(), battery, mirroring, inputType, busConflicts, subMapper, vsSystemType, ppuModel };
					if(entries.ContainsKey(crc)) {
						if(reliableDb && !reliableCrcs.Contains(crc)) {
							reliableCrcs.Add(crc);
							entries[crc] = entry;
							return crc;
						}

						List<string> prevData = entries[crc];
						for(int i = 0; i < entry.Count; i++) {

							if(prevData[i] == "" || forceOverride) {
								prevData[i] = entry[i];
							} else {
								if(entry[i] != prevData[i]) {
									if(i == 5 && entry[i] != "") {
										System.Diagnostics.Debug.Print(entry[i] + " != " + prevData[i]);
									}
									/*if(i == 1 || i == 2 || i == 3) {
										continue;
									}*/
									//System.Diagnostics.Debug.Print(entry[i] + " != " + prevData[i]);
								}
							}
						}
					} else {
						entries[crc] = entry;
						if(reliableDb) {
							reliableCrcs.Add(crc);
						}
					}
				}
			}
			return crc;
		}
	}
}
