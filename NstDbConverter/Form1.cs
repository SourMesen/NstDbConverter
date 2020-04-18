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

			Dictionary<string, GameEntry> entries = new Dictionary<string, GameEntry>();

			XmlDocument doc = new XmlDocument();
			/*doc.Load("GoodnesDb.xml");
			foreach(XmlNode node in doc.SelectNodes("database/game/cartridge")) {
				ProcessNode(entries, node, false, false);
			}*/

			doc = new XmlDocument();
			doc.Load("NesCartDB.xml");
			string lastCrc = "";
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

			doc = new XmlDocument();
			doc.Load("nes20db.xml");
			foreach(XmlNode node in doc.SelectNodes("nes20db/game")) {
				ProcessNewDbNode(entries, node);
			}

			List<string> lines = new List<string>();
			foreach(GameEntry entry in entries.Values) {
				lines.Add($"{entry.Crc},{entry.System},{entry.BoardType},{entry.PCB},{entry.Chip},{entry.MapperID},{entry.PrgRomSize},{entry.ChrRomSize},{entry.ChrRamSize},{entry.WramSize},{entry.SramSize},{entry.Battery},{entry.Mirroring},{entry.InputType},{entry.BusConflicts},{entry.SubMapper},{entry.VsSystemType},{entry.PpuModel}");
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
#     -NewRisingSun's NES 2.0 header database (2020-04-17)
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

			string fileContent = string.Join("\n", lines).Replace("\r\n", "\n").Replace("\n\r", "\n").Replace("\n", "\r\n");

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

		private void ProcessNewDbNode(Dictionary<string, GameEntry> entries, XmlNode node)
		{
			GameEntry entry = new GameEntry();

			entry.Crc = GetNodeAttribute(node, "rom", "crc32");
			GameEntry existingEntry = entries.ContainsKey(entry.Crc) ? entries[entry.Crc] : null;

			string type = GetNodeAttribute(node, "console", "type");
			string region = GetNodeAttribute(node, "console", "region");

			entry.System = "";
			switch(type) {
				case "0":
					switch(region) {
						case "0":
							if(existingEntry?.System == "Famicom") {
								entry.System = "Famicom";
							} else {
								entry.System = "NesNtsc";
							}
							break;
						case "1": entry.System = "NesPal"; break;
						case "2":
							if(existingEntry?.System == "Famicom") {
								entry.System = "Famicom";
							} else {
								entry.System = "NesNtsc";
							}
							break;
						case "3": entry.System = "Dendy"; break;
					}
					break;

				case "1": entry.System = "VsSystem"; break;
				case "2": entry.System = "Playchoice"; break;
				case "3": entry.System = "FamicloneDecimal"; break;
				case "4": entry.System = "VT01Mono"; break;
				case "5": entry.System = "VT01RedCyan"; break;
				case "6": entry.System = "VT02"; break;
				case "7": entry.System = "VT03"; break;
				case "8": entry.System = "VT09"; break;
				case "9": entry.System = "VT32"; break;
				case "10": entry.System = "VT369"; break;
				case "11": entry.System = "UM6578"; break;
				default: entry.System = "Other"; break;
			}

			entry.BoardType = existingEntry?.BoardType ?? "";
			if(entry.BoardType == "UNK") {
				entry.BoardType = "";
			}

			entry.PCB = existingEntry?.PCB ?? "";
			entry.Chip = existingEntry?.Chip ?? "";
			entry.MapperID = GetNodeAttribute(node, "pcb", "mapper");

			Func<string, string> getSize = (string attrValue) => {
				return (Int32.Parse(string.IsNullOrWhiteSpace(attrValue) ? "0" : attrValue) / 1024).ToString();
			};

			entry.PrgRomSize = getSize(GetNodeAttribute(node, "prgrom", "size"));
			entry.ChrRomSize = getSize(GetNodeAttribute(node, "chrrom", "size"));
			entry.ChrRamSize = getSize(GetNodeAttribute(node, "chrram", "size"));
			if(entry.ChrRamSize == "0") {
				//TODO, split this?
				entry.ChrRamSize = getSize(GetNodeAttribute(node, "chrnvram", "size"));
			}
			entry.WramSize = getSize(GetNodeAttribute(node, "prgram", "size"));
			entry.SramSize = getSize(GetNodeAttribute(node, "prgnvram", "size"));

			entry.Battery = GetNodeAttribute(node, "pcb", "battery");
			entry.Mirroring = GetNodeAttribute(node, "pcb", "mirroring").ToLower();
			entry.InputType = GetNodeAttribute(node, "expansion", "type");
			entry.BusConflicts = existingEntry?.BusConflicts ?? "";
			entry.SubMapper = GetNodeAttribute(node, "pcb", "submapper");
			entry.VsSystemType = GetNodeAttribute(node, "vs", "hardware");
			entry.PpuModel = GetNodeAttribute(node, "vs", "ppu");

			entries[entry.Crc] = entry;
			reliableCrcs.Add(entry.Crc);
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			this.Close();
		}

		private string ProcessNode(Dictionary<string,GameEntry> entries, XmlNode node, bool forceOverride, bool reliableDb, string lastCrc = "")
		{
			GameEntry entry = new GameEntry();
			string crc = node.Attributes["crc"] != null ? node.Attributes["crc"].Value.ToString() : "";
			if(crc == lastCrc) {
				return crc;
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

				bool isVsSystem = system == "VsSystem";
				bool isFamicom = (system == "Famicom" || system == "Dendy");

				string inputType = string.Empty;
				if(node.PreviousSibling != null && node.PreviousSibling.LocalName == "peripherals") {
					string device = GetNodeAttribute(node.PreviousSibling, "device", "type");
					switch(device) {
						case "zapper": inputType = isVsSystem ? "7" : "8"; break;
						case "fourplayer": inputType = isFamicom ? "3" : "2"; break;
						case "rob": inputType = 0x1F.ToString(); break;
						case "familyfunfitness": inputType = 0x0B.ToString(); break;
						case "suborkeyboard": inputType = 0x26.ToString(); break;
						case "partytap": inputType = 0x16.ToString(); break;
						case "powerpad": inputType = 0x0B.ToString(); break;
						//case "powerglove": inputType = "PowerGlove"; break;
						case "turbofile": inputType = 0x21.ToString(); break;
						case "konamihypershot": inputType = 0x12.ToString(); break;
						case "familytrainer": inputType = 0x0D.ToString(); break;
						case "arkanoid": inputType = isFamicom ? "16" : "15"; break;
						case "topriderbike": inputType = 0x1B.ToString(); break;
						case "barcodeworld": inputType = 0x18.ToString(); break;
						case "familykeyboard": inputType = 0x23.ToString(); break;
						case "mahjong": inputType = 0x15.ToString(); break;
						case "3dglasses": inputType = 0x1D.ToString(); break;
						case "miraclepiano": inputType = 0x19.ToString(); break;
						case "pokkunmoguraa": inputType = 0x1A.ToString(); break;
						case "excitingboxing": inputType = 0x14.ToString(); break;
						case "bandaihypershot": inputType = 0x0A.ToString(); break;
						//case "crazyclimber": inputType = "CrazyClimber"; break;
						case "battlebox": inputType = 0x22.ToString(); break;
						//case "aladdin": inputType = "Aladdin"; break;

						case "oekakids":
						case "oekakidstablet": inputType = 0x17.ToString(); break;

						case "pachinko": inputType = 0x13.ToString(); break;
						case "racermate": inputType = 0x2C.ToString(); break;
						case "uforce": inputType = 0x2D.ToString(); break;

						case "vsswapped": inputType = 0x05.ToString(); break;
						case "vsswapAB": inputType = 0x06.ToString(); break;

						default:
							inputType = "1";
							break;
					}
				}

				string ppuModel = node.Attributes["ppu"] != null ? node.Attributes["ppu"].Value.ToString() : "";
				switch(ppuModel) {
					case "RP2C04-0001": ppuModel = "2"; break;
					case "RP2C04-0002": ppuModel = "3"; break;
					case "RP2C04-0003": ppuModel = "4"; break;
					case "RP2C04-0004": ppuModel = "5"; break;
					case "RP2C05-01": ppuModel = "6"; break;
					case "RP2C05-02": ppuModel = "7"; break;
					case "RP2C05-03": ppuModel = "8"; break;
					case "RP2C05-04": ppuModel = "9"; break;
					case "RP2C05-05": ppuModel = "10"; break;
					case "RP2C03B": ppuModel = "1"; break;
					case "RP2C03G": ppuModel = "1"; break;
					default: ppuModel = ""; break;
				}

				if(vsSystemType == "") {
					vsSystemType = node.Attributes["protection"] != null ? node.Attributes["protection"].Value.ToString() : "";
					switch(vsSystemType) {
						case "RbiBaseball": vsSystemType = "1"; break;
						case "TkoBoxing": vsSystemType = "2"; break;
						case "SuperXevious": vsSystemType = "3"; break;
						case "IceClimber": vsSystemType = "4"; break;
						case "VsDualSystem": vsSystemType = "5"; break;
						case "RaidOnBungelingBay": vsSystemType = "6"; break;
						default: vsSystemType = ""; break;
					}
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

					entry = new GameEntry() {
						Crc = crc,
						System = system,
						BoardType = boardType,
						PCB = pcb,
						Chip = chip,
						MapperID = mapperID,
						PrgRomSize = prgRomSize.ToString(),
						ChrRomSize = chrRomSize == 0 ? "" : chrRomSize.ToString(),
						ChrRamSize = chrRamSize == 0 ? "" : chrRamSize.ToString(),
						WramSize = wramSize.ToString(),
						SramSize = sramSize.ToString(),
						Battery = battery,
						Mirroring = mirroring,
						InputType = inputType,
						BusConflicts = busConflicts,
						SubMapper = subMapper,
						VsSystemType = vsSystemType,
						PpuModel = ppuModel
					};

					if(entries.ContainsKey(crc)) {
						if(reliableDb && !reliableCrcs.Contains(crc)) {
							reliableCrcs.Add(crc);
							entries[crc] = entry;
							return crc;
						}

						GameEntry prevData = entries[crc];
						prevData.System = (forceOverride) ? entry.System : prevData.System;
						prevData.BoardType = (forceOverride) ? entry.BoardType : prevData.BoardType;
						prevData.PCB = (forceOverride) ? entry.PCB : prevData.PCB;
						prevData.Chip = (prevData.Chip == "" || forceOverride) ? entry.Chip : prevData.Chip;
						prevData.MapperID = (prevData.MapperID == "" || forceOverride) ? entry.MapperID : prevData.MapperID;
						prevData.PrgRomSize = (prevData.PrgRomSize == "" || forceOverride) ? entry.PrgRomSize : prevData.PrgRomSize;
						prevData.ChrRomSize = (prevData.ChrRomSize == "" || forceOverride) ? entry.ChrRomSize : prevData.ChrRomSize;
						prevData.ChrRamSize = (prevData.ChrRamSize == "" || forceOverride) ? entry.ChrRamSize : prevData.ChrRamSize;
						prevData.WramSize = (prevData.WramSize == "" || forceOverride) ? entry.WramSize : prevData.WramSize;
						prevData.SramSize = (prevData.SramSize == "" || forceOverride) ? entry.SramSize : prevData.SramSize;
						prevData.Battery = (prevData.Battery == "" || forceOverride) ? entry.Battery : prevData.Battery;
						prevData.Mirroring = (prevData.Mirroring == "" || forceOverride) ? entry.Mirroring : prevData.Mirroring;
						prevData.InputType = (prevData.InputType == "" || forceOverride) ? entry.InputType : prevData.InputType;
						prevData.BusConflicts = (prevData.BusConflicts == "" || forceOverride) ? entry.BusConflicts : prevData.BusConflicts;
						prevData.SubMapper = (prevData.SubMapper == "" || forceOverride) ? entry.SubMapper : prevData.SubMapper;
						prevData.VsSystemType = (prevData.VsSystemType == "" || forceOverride) ? entry.VsSystemType : prevData.VsSystemType;
						prevData.PpuModel = (prevData.PpuModel == "" || forceOverride) ? entry.PpuModel : prevData.PpuModel;
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

	class GameEntry
	{
		public string Crc;
		public string System;
		public string BoardType;
		public string PCB;
		public string Chip;
		public string MapperID;
		public string PrgRomSize;
		public string ChrRomSize;
		public string ChrRamSize;
		public string WramSize;
		public string SramSize;
		public string Battery;
		public string Mirroring;
		public string InputType;
		public string BusConflicts;
		public string SubMapper;
		public string VsSystemType;
		public string PpuModel;
	}
}
