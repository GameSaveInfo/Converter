using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Xml;
using GameSaveInfo;
namespace GSMConverter {
    class GameSaveManager: AConverter {

        public GameSaveManager(string xml): base(xml) {
            foreach (XmlNode entry in this.DocumentElement.ChildNodes) {
                switch (entry.Name) {
                    case "entry":
                        break;
                    default:
                        throw new NotSupportedException(entry.Name);    
                }
                loadEntry(entry);
            }
        }

        protected override string CleanUpInput(string input) {
            input = base.CleanUpInput(input);

            if (input.Contains("&reg;")) {
                input = input.Replace("&reg;", "®");
            }
            if (input.Contains("&trade;")) {
                input = input.Replace("&trade;", "™");
            }
                            
            return input;
        }

        private void loadEntry(XmlNode entry) {
            foreach (XmlAttribute attr in entry.Attributes) {
                switch (attr.Name) {
                    case "new":
                    case "id":
                        break;
                    default:
                        throw new NotSupportedException(attr.Name);
                }
            }
            string title = null, backupwarning, restorewarning;
            XmlElement dirs = null;
            XmlElement registry = null;
            foreach (XmlElement ele in entry.ChildNodes) {
                switch (ele.Name) {
                    case "title":
                        title = ele.InnerText;
                        break;
                    case "backupwarning":
                        backupwarning = ele.InnerText;
                        break;
                    case "restorewarning":
                        restorewarning = ele.InnerText;
                        break;
                    case "directories":
                        dirs = ele;
                        break;
                    case "registry":
                        registry = ele;
                        break;
                    case "lastmodified":
                        // We don't do anything with this
                        break;
                    default:
                        throw new NotSupportedException(ele.Name);
                }
            }
            string name = generateName(title);

            Game game = output.getGame(name);
            if (game == null) {
                game = new Game(name, title, null, false, GameType.game, output);
                output.Add(game);
            }


            if(dirs!=null)
                loadDirectories(dirs,game);


            if (registry != null)
                loadRegistries(registry, game);

            
        }


        private void loadDirectories(XmlElement dirs, Game game) {
            foreach (XmlElement dir in dirs.ChildNodes) {
                switch (dir.Name) {
                    case "dir":
                        loadDirectory(dir, game);
                        break;
                    default:
                        throw new NotSupportedException(dir.Name);
                }
            }
        }

        private void loadDirectory(XmlElement dir, Game game) {
            XmlElement path = null, reg = null;
            string include = null, exclude = null;
            foreach (XmlElement ele in dir.ChildNodes) {
                switch (ele.Name) {
                    case "path":
                        path = ele;
                        break;
                    case "include":
                        include = ele.InnerText;
                        break;
                    case "exclude":
                        exclude = ele.InnerText;
                        break;
                    case "reg":
                        reg = ele;
                        break;
                    default:
                        throw new NotSupportedException(ele.Name);
                }
            }


            string specialpath = path.Attributes["specialpath"].Value;

            ALocation loc = null;
            EnvironmentVariable ev =  EnvironmentVariable.None;
            
            RegRoot reg_root = RegRoot.none;
            string reg_key = null;
            string reg_value = null;

            string rel_path = path.InnerText;
            bool linkable = false;
            bool needs_file_path = false;
            switch (specialpath) {
                case "%REGISTRY%":
                    reg_root = getRegRoot(reg);
                    reg_key = getRegKey(reg);
                    reg_value = getRegValue(reg);
                    break;
                case "%APPDATA%":
                    ev = EnvironmentVariable.AppData;
                    linkable = true;
                    break;
                case "%DOCUMENTS%":
                    ev = EnvironmentVariable.UserDocuments;
                    linkable = true;
                    break;
                case "%APPDATA_COMMON%":
                    ev = EnvironmentVariable.CommonApplicationData;
                    linkable = true;
                    break;
                case "%APPDATA_LOCAL%":
                    ev = EnvironmentVariable.LocalAppData;
                    linkable = true;
                    break;
                case "%SAVED_GAMES%":
                    ev = EnvironmentVariable.SavedGames;
                    linkable = true;
                    break;
                case "%USER_PROFILE%":
                    ev = EnvironmentVariable.UserProfile;
                    linkable = true;
                    break;
                case "%SHARED_DOCUMENTS%":
                    ev = EnvironmentVariable.Public;
                    rel_path = System.IO.Path.Combine("Documents",rel_path);
                    linkable = true;
                    break;
                case "%STEAM_CLOUD%":
                    ev = EnvironmentVariable.SteamUserData;
                    break;
                case "%STEAM_CACHE%":
                    ev = EnvironmentVariable.SteamUser;
                    linkable = true;
                    break;
                case "%STEAM%":
                    if (rel_path.ToLower().StartsWith(@"steamapps\common\")) {
                        ev = EnvironmentVariable.SteamCommon;
                        rel_path = rel_path.Substring(17).Trim(System.IO.Path.DirectorySeparatorChar);
                    } else if (rel_path.ToLower().StartsWith(@"steamapps\sourcemods\")) {
                        ev = EnvironmentVariable.SteamSourceMods;
                        rel_path = rel_path.Substring(21).Trim(System.IO.Path.DirectorySeparatorChar);
                    } else {
                        throw new NotSupportedException(rel_path);
                    }
                    linkable = true;
                    needs_file_path = true;
                    break;
                case "%UPLAY%":
                    ev = EnvironmentVariable.UbisoftSaveStorage;
                    break;
                case "":
                    ev = EnvironmentVariable.Drive;
                    rel_path = rel_path.Substring(Path.GetPathRoot(rel_path).Length);
                    linkable = true;
                    needs_file_path = true;
                    break;
                default:
                    throw new NotSupportedException(specialpath);
            }
            GameVersion version;
            String path_prepend = null;
            rel_path = correctPath(rel_path);
            if(ev!= EnvironmentVariable.None) {
                switch (ev) {
                    case EnvironmentVariable.AppData:
                    case EnvironmentVariable.UserDocuments:
                    case EnvironmentVariable.CommonApplicationData:
                    case EnvironmentVariable.Public:
                    case EnvironmentVariable.SteamCommon:
                    case EnvironmentVariable.SavedGames:
                    case EnvironmentVariable.LocalAppData:
                    case EnvironmentVariable.UserProfile:
                    case EnvironmentVariable.SteamUser:
                    case EnvironmentVariable.SteamSourceMods:
                        if(!game.hasVersion("Windows",null,null))
                            game.addVersion("Windows",null,null);
                        version = game.getVersion("Windows",null,null);
                        break;
                    case EnvironmentVariable.SteamUserData:
                        if (!game.hasVersion(null, "SteamCloud", null))
                            game.addVersion(null, "SteamCloud", null);
                        version = game.getVersion(null, "SteamCloud", null);
                        break;
                    case EnvironmentVariable.UbisoftSaveStorage:
                        if (!game.hasVersion(null, "UbisoftSaveStorage", null))
                            game.addVersion(null, "UbisoftSaveStorage", null);
                        version = game.getVersion(null, "UbisoftSaveStorage", null);
                        break;
                    default:
                        throw new NotSupportedException(ev.ToString());
                }
                switch (ev) {
                    case EnvironmentVariable.SteamUser:
                    case EnvironmentVariable.SteamCommon:
                    case EnvironmentVariable.SteamSourceMods:
                    case EnvironmentVariable.SteamUserData:
                        if (rel_path.Contains('\\')) {
                            int index = rel_path.IndexOf('\\');
                            path_prepend = rel_path.Substring(index+1);
                            rel_path = rel_path.Substring(0, index);
                        }
                        break;
                }

                loc = new LocationPath(version.Locations,ev, rel_path);
            } else {
                if(!game.hasVersion("Windows",null,null))
                    game.addVersion("Windows",null,null);
                version = game.getVersion("Windows",null,null);

                loc = new LocationRegistry(version.Locations, reg_root.ToString(), correctReg(reg_key), reg_value);
                if (rel_path != null && rel_path != "")
                    path_prepend = correctPath(rel_path);
            }
            version.addLocation(loc);

            FileType type = version.addFileType(null);

            foreach (string inc in include.Split('|')) {
                Include save;
                string add_path = null;
                if (needs_file_path)
                    add_path = "";

                if (path_prepend != null) {
                    add_path = path_prepend;
                }

                string file = correctPath(inc);
                if (file.Contains('\\')) {
                    string[] splits = file.Split('\\');
                    for (int i = 0; i < splits.Length; i++) {
                        if (i < splits.Length - 1) {
                            if (add_path != null)
                                add_path = Path.Combine(add_path, splits[i]);
                            else
                                add_path = splits[i];
                        } else {
                            file = splits[i];
                        }
                    }
                }

                if (file == "*.*"||file=="*") {
                    save = type.addSave(add_path, null);
                } else {
                    save = type.addSave(add_path, file);
                }

                foreach (string exc in exclude.Split('|')) {
                    if (exc != "") {
                        save.addExclusion(add_path, exc);
                    }
                }

            }

            if (linkable) {
                if(path_prepend!=null) {
                    version.addLink(path_prepend);
                }else if (needs_file_path) {
                        version.addLink("");
                } else
                    version.addLink(null);
            }

            version.addContributor("GameSaveManager");

        }
        private string correctReg(string path) {
            if (path.ToLower().Contains("Wow6432Node".ToLower())) {
                string pth = path.Replace("Wow6432Node", "");
                if (pth == path)
                    throw new Exception("replacement didn't affect string");
                path = pth;
            }

            path = correctPath(path);

            return path;
        }
        private string correctPath(string path) {
            if (path.Contains('/'))
                path = path.Replace('/','\\');
            // Correct doubles
            if (path.Contains("//"))
                path = path.Replace("//", "\\");

            if (path.Contains("\\\\"))
                path = path.Replace("\\\\", "\\");

            return path;
        }

        private RegRoot getRegRoot(XmlElement reg) {
            foreach (XmlElement ele in reg) {
                switch (ele.Name) {
                    case "hive":
                        return LocationRegistry.parseRegRoot(ele.InnerText);
                    case "path":
                        break;
                    case "value":
                        break;
                    default:
                        throw new NotSupportedException(ele.Name);
                }
            }
            throw new KeyNotFoundException();
        }

        private string getRegKey(XmlElement reg) {
            foreach (XmlElement ele in reg) {
                switch (ele.Name) {
                    case "hive":
                        break;
                    case "path":
                        return ele.InnerText;
                    case "value":
                        break;
                    default:
                        throw new NotSupportedException(ele.Name);
                }
            }
            throw new KeyNotFoundException();
        }
        private string getRegValue(XmlElement reg) {
            foreach (XmlElement ele in reg) {
                switch (ele.Name) {
                    case "hive":
                        break;
                    case "path":
                        break;
                    case "value":
                        return ele.InnerText;
                    default:
                        throw new NotSupportedException(ele.Name);
                }
            }
            return null;
        }

        private void loadRegistries(XmlElement reg, Game game) {
            foreach (XmlElement ele in reg.ChildNodes) {
                switch (ele.Name) {
                    case "reg":
                        loadRegistry(ele, game);
                        break;
                    default:
                        throw new NotSupportedException(ele.Name);
                }
            }
        }
        private void loadRegistry(XmlElement reg, Game game) {
            RegRoot root = RegRoot.none;;
            string key = null, values = null;

            foreach (XmlElement ele in reg.ChildNodes) {
                switch (ele.Name) {
                    case "hive":
                        root = LocationRegistry.parseRegRoot(ele.InnerText);
                        break;
                    case "path":
                        key = ele.InnerText;
                        break;
                    case "values":
                        values = ele.InnerText;
                        break;
                    default:
                        throw new NotSupportedException(ele.Name);
                }
            }
            
            if(!game.hasVersion("Windows",null,null))
                game.addVersion("Windows",null,null);

            GameVersion version = game.getVersion("Windows",null,null);

            version.addRegEntry(root, key, values, "Saves");

        }


    }
}
