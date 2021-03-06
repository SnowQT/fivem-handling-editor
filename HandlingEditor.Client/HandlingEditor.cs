﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Drawing;
using System.Text;
using NativeUI;
using CitizenFX.Core;
using CitizenFX.Core.UI;
using static CitizenFX.Core.Native.API;
using System.Xml;

namespace HandlingEditor.Client
{
    public class HandlingEditor : BaseScript
    {
        private static string ResourceName;
        private static readonly string ScriptName = "Handling Editor";
        private static readonly string kvpPrefix = "handling_";

        #region CONFIG_FIEDS
        private static float editingFactor = 0.01f;
        private static float maxSyncDistance = 150.0f;
        private static long timer = 1000;
        private static bool debug = false;
        private static int toggleMenu = 168;
        private static float screenPosX = 1.0f;
        private static float screenPosY = 0.0f;
        #endregion

        #region FIELDS
        private HandlingInfo handlingInfo;
        private Dictionary<string,HandlingPreset> serverPresets;
        private long currentTime;
        private long lastTime;
        private int playerPed;
        private int currentVehicle;
        private HandlingPreset currentPreset;
        private IEnumerable<int> vehicles;
        #endregion

        #region GUI_FIELDS
        private MenuPool _menuPool;
        private UIMenu EditorMenu;
        private UIMenu presetsMenu;
        private UIMenu serverPresetsMenu;
        #endregion

        #region GUI_METHODS
        private async Task<string> GetOnScreenString(string defaultText)
        {
            DisableAllControlActions(1);
            AddTextEntry("ENTER_VALUE", "Enter value");
            DisplayOnscreenKeyboard(1, "ENTER_VALUE", "", defaultText, "", "", "", 128);
            while (UpdateOnscreenKeyboard() != 1 && UpdateOnscreenKeyboard() != 2) await Delay(0);
            EnableAllControlActions(1);
            return GetOnscreenKeyboardResult();
        }

        private UIMenuDynamicListItem AddDynamicFloatList(UIMenu menu, FloatFieldInfo fieldInfo)
        {
            string name = fieldInfo.Name;
            string description = fieldInfo.Description;
            float min = fieldInfo.Min;
            float max = fieldInfo.Max;

            if (!currentPreset.Fields.ContainsKey(name))
                return null;

            float value = currentPreset.Fields[name];
            var newitem = new UIMenuDynamicListItem(name, description, value.ToString("F3"), (sender, direction) =>
            {
                if (direction == UIMenuDynamicListItem.ChangeDirection.Left)
                {
                    var newvalue = value - editingFactor;
                    if (newvalue < min)
                        CitizenFX.Core.UI.Screen.ShowNotification($"Min value allowed for ~b~{name}~w~ is {min}");
                    else
                    {
                        value = newvalue;
                        currentPreset.Fields[name] = newvalue;
                    }
                }
                else
                {
                    var newvalue = value + editingFactor;
                    if (newvalue > max)
                        CitizenFX.Core.UI.Screen.ShowNotification($"Max value allowed for ~b~{name}~w~ is {max}");
                    else
                    {
                        value = newvalue;
                        currentPreset.Fields[name] = newvalue;
                    }
                }
                return value.ToString("F3");
            });

            menu.AddItem(newitem);

            EditorMenu.OnItemSelect += async (sender, item, index) =>
            {
                if (item == newitem)
                {
                    EditorMenu.Visible = false;

                    string text = await GetOnScreenString(value.ToString());
                    float newvalue = value;

                    if (float.TryParse(text, out newvalue))
                    {
                        if(newvalue >= min && newvalue <= max)
                            currentPreset.Fields[name] = newvalue;
                        else
                            CitizenFX.Core.UI.Screen.ShowNotification($"Value out of allowed limits for ~b~{name}~w~, Min:{min}, Max:{max}");
                    }else
                        CitizenFX.Core.UI.Screen.ShowNotification($"Invalid value for ~b~{name}~w~");

                    int currentSelection = EditorMenu.CurrentSelection;
                    InitialiseMenu(); //Should just update the current item instead
                    EditorMenu.CurrentSelection = currentSelection;
                    EditorMenu.Visible = true;
                }
            };

            return newitem;
        }

        private UIMenuDynamicListItem AddDynamicIntList(UIMenu menu, IntFieldInfo fieldInfo)
        {
            string name = fieldInfo.Name;
            string description = fieldInfo.Description;
            int min = fieldInfo.Min;
            int max = fieldInfo.Max;

            if (!currentPreset.Fields.ContainsKey(name))
                return null;

            int value = currentPreset.Fields[name]; //TODO: Get value from current preset
            var newitem = new UIMenuDynamicListItem(name, description, value.ToString(), (sender, direction) =>
            {
                if (direction == UIMenuDynamicListItem.ChangeDirection.Left)
                {
                    var newvalue = value - 1;
                    if (newvalue < min)
                        CitizenFX.Core.UI.Screen.ShowNotification($"Min value allowed for ~b~{name}~w~ is {min}");
                    else
                    {
                        value = newvalue;
                        currentPreset.Fields[name] = newvalue;
                    }
                }
                else
                {
                    var newvalue = value + 1;
                    if (newvalue > max)
                        CitizenFX.Core.UI.Screen.ShowNotification($"Max value allowed for ~b~{name}~w~ is {max}");
                    else
                    {
                        value = newvalue;
                        currentPreset.Fields[name] = newvalue;
                    }
                }
                return value.ToString();
            });

            menu.AddItem(newitem);

            EditorMenu.OnItemSelect += async (sender, item, index) =>
            {
                if (item == newitem)
                {
                    EditorMenu.Visible = false;

                    string text = await GetOnScreenString(value.ToString());
                    int newvalue = value;

                    if (int.TryParse(text, out newvalue))
                    {
                        if (newvalue >= min && newvalue <= max)
                            currentPreset.Fields[name] = newvalue;
                        else
                            CitizenFX.Core.UI.Screen.ShowNotification($"Value out of allowed limits for ~b~{name}~w~, Min:{min}, Max:{max}");
                    }
                    else
                        CitizenFX.Core.UI.Screen.ShowNotification($"Invalid value for ~b~{name}~w~");

                    int currentSelection = EditorMenu.CurrentSelection;
                    InitialiseMenu(); //Should just update the current item instead
                    EditorMenu.CurrentSelection = currentSelection;
                    EditorMenu.Visible = true;
                }
            };

            return newitem;
        }

        private void AddDynamicVector3List(UIMenu menu, VectorFieldInfo fieldInfo)
        {
            if (!currentPreset.Fields.ContainsKey(fieldInfo.Name))
                return;

            string fieldDescription = fieldInfo.Description;
            string fieldNameX = $"{fieldInfo.Name}_x";
            float valueX = currentPreset.Fields[fieldInfo.Name].X;
            float minValueX = fieldInfo.Min.X;
            float maxValueX = fieldInfo.Max.X;

            var newitemX = new UIMenuDynamicListItem(fieldNameX, fieldDescription, valueX.ToString("F3"), (sender, direction) =>
            {
                if (direction == UIMenuDynamicListItem.ChangeDirection.Left)
                {
                    var newvalue = valueX - editingFactor;
                    if (newvalue < minValueX)
                        CitizenFX.Core.UI.Screen.ShowNotification($"Min value allowed for ~b~{fieldNameX}~w~ is {minValueX}");
                    else
                    {
                        valueX = newvalue;
                        currentPreset.Fields[fieldInfo.Name].X = newvalue;
                    }
                }
                else
                {
                    var newvalue = valueX + editingFactor;
                    if (newvalue > maxValueX)
                        CitizenFX.Core.UI.Screen.ShowNotification($"Max value allowed for ~b~{fieldNameX}~w~ is {maxValueX}");
                    else
                    {
                        valueX = newvalue;
                        currentPreset.Fields[fieldInfo.Name].X = newvalue;
                    }
                }
                return valueX.ToString("F3");
            });

            menu.AddItem(newitemX);

            EditorMenu.OnItemSelect += async (sender, item, index) =>
            {
                if (item == newitemX)
                {
                    EditorMenu.Visible = false;

                    string text = await GetOnScreenString(valueX.ToString());
                    float newvalue = valueX;

                    if (float.TryParse(text, out newvalue))
                    {
                        if (newvalue >= minValueX && newvalue <= maxValueX)
                            currentPreset.Fields[fieldInfo.Name].X = newvalue;
                        else
                            CitizenFX.Core.UI.Screen.ShowNotification($"Value out of allowed limits for ~b~{fieldNameX}~w~, Min:{minValueX}, Max:{maxValueX}");
                    }
                    else
                        CitizenFX.Core.UI.Screen.ShowNotification($"Invalid value for ~b~{fieldNameX}~w~");

                    int currentSelection = EditorMenu.CurrentSelection;
                    InitialiseMenu(); //Should just update the current item instead
                    EditorMenu.CurrentSelection = currentSelection;
                    EditorMenu.Visible = true;
                }
            };

            string fieldNameY = $"{fieldInfo.Name}_y";
            float valueY = currentPreset.Fields[fieldInfo.Name].Y;
            float minValueY = fieldInfo.Min.X;
            float maxValueY = fieldInfo.Max.X;

            var newitemY = new UIMenuDynamicListItem(fieldNameY, fieldDescription, valueY.ToString("F3"), (sender, direction) =>
            {
                if (direction == UIMenuDynamicListItem.ChangeDirection.Left)
                {
                    var newvalue = valueY - editingFactor;
                    if (newvalue < minValueY)
                        CitizenFX.Core.UI.Screen.ShowNotification($"Min value allowed for ~b~{fieldNameY}~w~ is {minValueY}");
                    else
                    {
                        valueY = newvalue;
                        currentPreset.Fields[fieldInfo.Name].Y = newvalue;
                    }
                }
                else
                {
                    var newvalue = valueY + editingFactor;
                    if (newvalue > maxValueY)
                        CitizenFX.Core.UI.Screen.ShowNotification($"Max value allowed for ~b~{fieldNameY}~w~ is {maxValueY}");
                    else
                    {
                        valueY = newvalue;
                        currentPreset.Fields[fieldInfo.Name].Y = newvalue;
                    }
                }
                return valueY.ToString("F3");
            });

            menu.AddItem(newitemY);

            EditorMenu.OnItemSelect += async (sender, item, index) =>
            {
                if (item == newitemY)
                {
                    EditorMenu.Visible = false;

                    string text = await GetOnScreenString(valueY.ToString());
                    float newvalue = valueY;

                    if (float.TryParse(text, out newvalue))
                    {
                        if (newvalue >= minValueY && newvalue <= maxValueY)
                            currentPreset.Fields[fieldInfo.Name].Y = newvalue;
                        else
                            CitizenFX.Core.UI.Screen.ShowNotification($"Value out of allowed limits for ~b~{fieldNameY}~w~, Min:{minValueY}, Max:{maxValueY}");
                    }
                    else
                        CitizenFX.Core.UI.Screen.ShowNotification($"Invalid value for ~b~{fieldNameY}~w~");

                    int currentSelection = EditorMenu.CurrentSelection;
                    InitialiseMenu(); //Should just update the current item instead
                    EditorMenu.CurrentSelection = currentSelection;
                    EditorMenu.Visible = true;
                }
            };

            string fieldNameZ = $"{fieldInfo.Name}_z";
            float valueZ = currentPreset.Fields[fieldInfo.Name].Z;
            float minValueZ = fieldInfo.Min.Z;
            float maxValueZ = fieldInfo.Max.Z;

            var newitemZ = new UIMenuDynamicListItem(fieldNameZ, fieldDescription, valueZ.ToString("F3"), (sender, direction) =>
            {
                if (direction == UIMenuDynamicListItem.ChangeDirection.Left)
                {
                    var newvalue = valueZ - editingFactor;
                    if (newvalue < minValueZ)
                        CitizenFX.Core.UI.Screen.ShowNotification($"Min value allowed for ~b~{fieldNameZ}~w~ is {minValueZ}");
                    else
                    {
                        valueZ = newvalue;
                        currentPreset.Fields[fieldInfo.Name].Z = newvalue;
                    }
                }
                else
                {
                    var newvalue = valueZ + editingFactor;
                    if (newvalue > maxValueZ)
                        CitizenFX.Core.UI.Screen.ShowNotification($"Max value allowed for ~b~{fieldNameZ}~w~ is {maxValueZ}");
                    else
                    {
                        valueZ = newvalue;
                        currentPreset.Fields[fieldInfo.Name].Z = newvalue;
                    }
                }
                return valueZ.ToString("F3");
            });

            menu.AddItem(newitemZ);

            EditorMenu.OnItemSelect += async (sender, item, index) =>
            {
                if (item == newitemZ)
                {
                    EditorMenu.Visible = false;

                    string text = await GetOnScreenString(valueZ.ToString());
                    float newvalue = valueZ;

                    if (float.TryParse(text, out newvalue))
                    {
                        if (newvalue >= minValueZ && newvalue <= maxValueZ)
                            currentPreset.Fields[fieldInfo.Name].Z = newvalue;
                        else
                            CitizenFX.Core.UI.Screen.ShowNotification($"Value out of allowed limits for ~b~{fieldNameZ}~w~, Min:{minValueZ}, Max:{maxValueZ}");
                    }
                    else
                        CitizenFX.Core.UI.Screen.ShowNotification($"Invalid value for ~b~{fieldNameZ}~w~");

                    int currentSelection = EditorMenu.CurrentSelection;
                    InitialiseMenu(); //Should just update the current item instead
                    EditorMenu.CurrentSelection = currentSelection;
                    EditorMenu.Visible = true;
                }
            };
        }
        
        private UIMenuItem AddMenuReset(UIMenu menu)
        {
            var newitem = new UIMenuItem("Reset", "Restores the default values");
            menu.AddItem(newitem);

            menu.OnItemSelect += (sender, item, index) =>
            {
                if (item == newitem)
                {
                    currentPreset.Reset();
                    RefreshVehicleUsingPreset(currentVehicle, currentPreset);
                    RemoveDecorators(currentVehicle);

                    InitialiseMenu();
                    EditorMenu.Visible = true;
                }
            };
            return newitem;
        }

        private UIMenuItem AddLockedItem(UIMenu menu, FieldInfo fieldInfo)
        {
            var newitem = new UIMenuItem(fieldInfo.Name, fieldInfo.Description);
            newitem.Enabled = false;
            newitem.SetRightBadge(UIMenuItem.BadgeStyle.Lock);

            menu.AddItem(newitem);

            menu.OnItemSelect += (sender, item, index) =>
            {
                if (item == newitem)
                {
                    CitizenFX.Core.UI.Screen.ShowNotification($"The server doesn't allow to edit this field.");
                }
            };
            return newitem;
        }

        private UIMenu AddPresetsSubMenu(UIMenu menu)
        {
            var newitem = _menuPool.AddSubMenu(menu, "Saved Presets", "The handling presets saved by you.");
            {
                newitem.MouseEdgeEnabled = false;
                newitem.ControlDisablingEnabled = false;
                newitem.MouseControlsEnabled = false;
                newitem.AddInstructionalButton(new InstructionalButton(Control.PhoneExtraOption, "Save"));
                newitem.AddInstructionalButton(new InstructionalButton(Control.PhoneOption, "Delete"));
            }

            KvpList kvpList = new KvpList(kvpPrefix);
            foreach(var key in kvpList)
            {
                string value = GetResourceKvpString(key);
                newitem.AddItem(new UIMenuItem(key.Remove(0, kvpPrefix.Length)));  
            }
            return newitem;
        }

        private UIMenu AddServerPresetsSubMenu(UIMenu menu)
        {
            var newitem = _menuPool.AddSubMenu(menu, "Server Presets", "The handling presets loaded from the server.");
            {
                newitem.MouseEdgeEnabled = false;
                newitem.ControlDisablingEnabled = false;
                newitem.MouseControlsEnabled = false;
            }

            foreach (var preset in serverPresets)
                newitem.AddItem(new UIMenuItem(preset.Key));

            return newitem;
        }

        private async void InitialiseMenu()
        {
            _menuPool = new MenuPool();
            {
                _menuPool.ResetCursorOnOpen = true;
            }

            EditorMenu = new UIMenu(ScriptName, "Beta", new PointF(screenPosX * Screen.Width, screenPosY * Screen.Height));
            {
                EditorMenu.MouseEdgeEnabled = false;
                EditorMenu.ControlDisablingEnabled = false;
                EditorMenu.MouseControlsEnabled = false;
            }

            foreach (var item in handlingInfo.FieldsInfo)
            {
                var fieldInfo = item.Value;

                if(fieldInfo.Editable)
                {
                    /*
                    string fieldName = fieldInfo.Name;
                    string fieldDescription = fieldInfo.Description;*/
                    Type fieldType = fieldInfo.Type;

                    if (fieldType == FieldType.FloatType)
                        AddDynamicFloatList(EditorMenu, (FloatFieldInfo)item.Value);
                    else if (fieldType == FieldType.IntType)
                        AddDynamicIntList(EditorMenu, (IntFieldInfo)item.Value);
                    else if (fieldType == FieldType.Vector3Type)
                        AddDynamicVector3List(EditorMenu, (VectorFieldInfo)item.Value);
                }
                else
                {
                    //AddLockedItem(EditorMenu, item.Value);
                }
            }

            AddMenuReset(EditorMenu);
            presetsMenu = AddPresetsSubMenu(EditorMenu);
            serverPresetsMenu = AddServerPresetsSubMenu(EditorMenu);

            presetsMenu.OnItemSelect += (sender, item, index) =>
            {
                if(sender == presetsMenu)
                {
                    string key = $"{kvpPrefix}{item.Text}";
                    string value = GetResourceKvpString(key);
                    if (value != null)
                    {
                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(value);
                        var handling = doc["Item"];
                        GetPresetFromXml(handling, currentPreset);

                        CitizenFX.Core.UI.Screen.ShowNotification($"Personal preset ~b~{item.Text}~w~ applied");
                        InitialiseMenu();
                        presetsMenu.Visible = true;
                    }
                    else
                        CitizenFX.Core.UI.Screen.ShowNotification($"~r~ERROR~w~: Personal preset ~b~{item.Text}~w~ corrupted");
                }
            };

            serverPresetsMenu.OnItemSelect += (sender, item, index) =>
            {
                if(sender == serverPresetsMenu)
                {
                    string key = item.Text;
                    if (serverPresets.ContainsKey(key))
                    {
                        foreach (var field in serverPresets[key].Fields.Keys)
                        {
                            if (currentPreset.Fields.ContainsKey(field))
                            {
                                currentPreset.Fields[field] = serverPresets[key].Fields[field];
                            }
                            else Debug.Write($"Missing {field} field in currentPreset");
                        }
                        CitizenFX.Core.UI.Screen.ShowNotification($"Server preset ~b~{key}~w~ applied");
                        InitialiseMenu();
                        serverPresetsMenu.Visible = true;
                    }
                    else
                        CitizenFX.Core.UI.Screen.ShowNotification($"~r~ERROR~w~: Server preset ~b~{key}~w~ corrupted");
                }
            };

            _menuPool.Add(EditorMenu);
            _menuPool.RefreshIndex();

            await Delay(0);
        }
        #endregion

        public HandlingEditor()
        {
            ResourceName = GetCurrentResourceName();
            Debug.WriteLine($"{ScriptName}: Script by Neos7");
            LoadConfig();
            
            handlingInfo = new HandlingInfo();
            ReadFieldInfo();
            serverPresets = new Dictionary<string, HandlingPreset>();
            ReadServerPresets();

            RegisterDecorators();

            currentTime = GetGameTimer();
            lastTime = currentTime;
            currentPreset = null;
            currentVehicle = -1;
            vehicles = Enumerable.Empty<int>();

            RegisterCommand("handling_distance", new Action<int, dynamic>((source, args) =>
            {
                if(args.Count < 1)
                {
                    Debug.WriteLine($"{ScriptName}: Missing float argument");
                    return;
                }

                if (float.TryParse(args[0], out float value))
                {
                    maxSyncDistance = value;
                    Debug.WriteLine($"{ScriptName}: Received new {nameof(maxSyncDistance)} value {value}");
                }
                else Debug.WriteLine($"{ScriptName}: Error parsing {args[0]} as float");

            }), false);

            RegisterCommand("handling_debug", new Action<int, dynamic>((source, args) =>
            {
                if (args.Count < 1)
                {
                    Debug.WriteLine($"{ScriptName}: Missing bool argument");
                    return;
                }

                if (bool.TryParse(args[0], out bool value))
                {
                    debug = value;
                    Debug.WriteLine($"{ScriptName}: Received new {nameof(debug)} value {value}");
                }
                else Debug.WriteLine($"{ScriptName}: Error parsing {args[0]} as bool");

            }), false);

            RegisterCommand("handling_decorators", new Action<int, dynamic>((source, args) =>
            {
                if (args.Count < 1)
                    PrintDecorators(currentVehicle);
                else
                {
                    if (int.TryParse(args[0], out int value))
                        PrintDecorators(value);
                    else Debug.WriteLine($"{ScriptName}: Error parsing {args[0]} as int");
                }

            }), false);

            RegisterCommand("handling_print", new Action<int, dynamic>((source, args) =>
            {
                PrintVehiclesWithDecorators(vehicles);
            }), false);

            RegisterCommand("handling_preset", new Action<int, dynamic>((source, args) =>
            {
                if (currentPreset != null)
                    Debug.WriteLine(currentPreset.ToString());
                else
                    Debug.WriteLine($"{ScriptName}: Current preset doesn't exist");
            }), false);

            Tick += OnTick;
            Tick += ScriptTask;
        }

        /// <summary>
        /// The GUI task of the script
        /// </summary>
        /// <returns></returns>
        private async Task OnTick()
        {
            if(_menuPool != null)
            {

                _menuPool.ProcessMenus();

                if (_menuPool.IsAnyMenuOpen())
                    DisableControls();

                if (currentVehicle != -1)
                {
                    if (IsControlJustPressed(1, toggleMenu)/* || IsDisabledControlJustPressed(1, toggleMenu)*/) // TOGGLE MENU VISIBLE
                    {
                        if (!EditorMenu.Visible && !_menuPool.IsAnyMenuOpen())
                            EditorMenu.Visible = true;
                        else if (_menuPool.IsAnyMenuOpen())
                            _menuPool.CloseAllMenus();
                    }

                    if (presetsMenu.Visible)
                    {
                        DisableControlAction(1, 75, true); // INPUT_VEH_EXIT - Y
                        DisableControlAction(1, 37, true); // INPUT_SELECT_WEAPON - X

                        if (IsControlJustPressed(1, 179))
                        {
                            string name = await GetOnScreenString("");
                            if (!string.IsNullOrEmpty(name))
                            {
                                SavePreset(name, currentPreset);
                                InitialiseMenu();
                                presetsMenu.Visible = true;
                            }
                            else
                                CitizenFX.Core.UI.Screen.ShowNotification("Invalid string.");
                        }
                        else if (IsControlJustPressed(1, 178))
                        {
                            if (presetsMenu.MenuItems.Count > 0)
                            {
                                string key = $"{kvpPrefix}{presetsMenu.MenuItems[presetsMenu.CurrentSelection].Text}";
                                if (GetResourceKvpString(key) != null)
                                {
                                    DeleteResourceKvp(key);
                                    InitialiseMenu();
                                    presetsMenu.Visible = true;
                                }
                            }
                            else
                                CitizenFX.Core.UI.Screen.ShowNotification("Nothing to delete.");
                        }
                    }

                }
                else
                {
                    if (_menuPool.IsAnyMenuOpen())
                        _menuPool.CloseAllMenus();
                }
            }

            await Task.FromResult(0);
        }

        /// <summary>
        /// Disable controls for controller to use the script with the controller
        /// </summary>
        private async void DisableControls()
        {
            DisableControlAction(1, 85, true); // INPUT_VEH_RADIO_WHEEL = DPAD - LEFT
            DisableControlAction(1, 74, true); // INPUT_VEH_HEADLIGHT = DPAD - RIGHT
            DisableControlAction(1, 48, true); // INPUT_HUD_SPECIAL = DPAD - DOWN
            DisableControlAction(1, 27, true); // INPUT_PHONE = DPAD - UP
            DisableControlAction(1, 80, true); // INPUT_VEH_CIN_CAM = B
            DisableControlAction(1, 73, true); // INPUT_VEH_DUCK = A

            await Delay(0);
        }

        /// <summary>
        /// The main task of the script
        /// </summary>
        /// <returns></returns>
        private async Task ScriptTask()
        {
            currentTime = (GetGameTimer() - lastTime);

            playerPed = PlayerPedId();

            if (IsPedInAnyVehicle(playerPed, false))
            {
                int vehicle = GetVehiclePedIsIn(playerPed, false);

                if (IsThisModelACar((uint)GetEntityModel(vehicle)) && GetPedInVehicleSeat(vehicle, -1) == playerPed && !IsEntityDead(vehicle))
                {
                    // Update current vehicle and get its preset
                    if (vehicle != currentVehicle)
                    {
                        currentVehicle = vehicle;
                        currentPreset = CreateHandlingPreset(currentVehicle);                
                        InitialiseMenu();
                    }
                }
                else
                {
                    // If current vehicle isn't a car or player isn't driving current vehicle or vehicle is dead
                    currentVehicle = -1;
                    currentPreset = null;
                }
            }
            else
            {
                // If player isn't in any vehicle
                currentVehicle = -1;
                currentPreset = null;
            }

            // Check if decorators needs to be updated
            if (currentTime > timer)
            {
                // Current vehicle could be updated each tick to show the edited fields live
                // Check if current vehicle needs to be refreshed
                if (currentVehicle != -1 && currentPreset != null)
                {
                    if (currentPreset.IsEdited)
                        RefreshVehicleUsingPreset(currentVehicle, currentPreset);
                }

                if (currentVehicle != -1 && currentPreset != null)
                    UpdateVehicleDecorators(currentVehicle, currentPreset);

                vehicles = new VehicleList();

                // Refreshes the iterated vehicles
                RefreshVehicles(vehicles.Except(new List<int> { currentVehicle }));

                lastTime = GetGameTimer();
            }
            await Delay(0);
        }

        /// <summary>
        /// Refreshes the handling for the <paramref name="vehicle"/> using the <paramref name="preset"/>.
        /// </summary>
        /// <param name="vehicle"></param>
        /// <param name="preset"></param>
        private async void RefreshVehicleUsingPreset(int vehicle, HandlingPreset preset)
        {
            if (DoesEntityExist(vehicle))
            {
                foreach (var item in preset.Fields)
                {
                    string fieldName = item.Key;
                    dynamic fieldValue = item.Value;

                    var fieldsInfo = handlingInfo.FieldsInfo;
                    if (!fieldsInfo.ContainsKey(fieldName))
                    {
                        if (debug)
                            Debug.WriteLine($"{ScriptName}: No fieldInfo definition found for {fieldName}");
                        continue;
                    }

                    FieldInfo fieldInfo = fieldsInfo[fieldName];
                    Type fieldType = fieldInfo.Type;
                    string className = fieldInfo.ClassName;

                    if (fieldType == FieldType.FloatType)
                    {
                        var value = GetVehicleHandlingFloat(vehicle, className, fieldName);
                        if (Math.Abs(value - fieldValue) > 0.001f)
                        {
                            SetVehicleHandlingFloat(vehicle, className, fieldName, fieldValue);

                            if (debug)
                                Debug.WriteLine($"{ScriptName}: {fieldName} updated from {value} to {fieldValue}");
                        }     
                    }
                    
                    else if (fieldType == FieldType.IntType)
                    {
                        var value = GetVehicleHandlingInt(vehicle, className, fieldName);
                        if (value != fieldValue)
                        {
                            SetVehicleHandlingInt(vehicle, className, fieldName, fieldValue);

                            if (debug)
                                Debug.WriteLine($"{ScriptName}: {fieldName} updated from {value} to {fieldValue}");
                        }
                    }
                    
                    else if (fieldType == FieldType.Vector3Type)
                    {
                        var value = GetVehicleHandlingVector(vehicle, className, fieldName);
                        if (value != fieldValue)
                        {
                            SetVehicleHandlingVector(vehicle, className, fieldName, fieldValue);

                            if (debug)
                                Debug.WriteLine($"{ScriptName}: {fieldName} updated from {value} to {fieldValue}");
                        }
                    }
                }
            }
            await Delay(0);
        }

        /// <summary>
        /// Refreshes the handling for the vehicles in <paramref name="vehiclesList"/> if they are close enough.
        /// </summary>
        /// <param name="vehiclesList"></param>
        private async void RefreshVehicles(IEnumerable<int> vehiclesList)
        {
            Vector3 currentCoords = GetEntityCoords(playerPed, true);

            foreach (int entity in vehiclesList)
            {
                if (DoesEntityExist(entity))
                {
                    Vector3 coords = GetEntityCoords(entity, true);

                    if (Vector3.Distance(currentCoords, coords) <= maxSyncDistance)
                        RefreshVehicleUsingDecorators(entity);
                }
            }
            await Delay(0);
        }

        /// <summary>
        /// Refreshes the handling for the <paramref name="vehicle"/> using the decorators attached to it.
        /// </summary>
        /// <param name="vehicle"></param>
        private async void RefreshVehicleUsingDecorators(int vehicle)
        {
            foreach (var item in handlingInfo.FieldsInfo.Where(a => a.Value.Editable))
            {
                string fieldName = item.Key;
                Type fieldType = item.Value.Type;
                string className = item.Value.ClassName;

                if (fieldType == FieldType.FloatType)
                {
                    if (DecorExistOn(vehicle, fieldName))
                    {
                        var decorValue = DecorGetFloat(vehicle, fieldName);
                        var value = GetVehicleHandlingFloat(vehicle, className, fieldName);
                        if (Math.Abs(value - decorValue) > 0.001f)
                        {
                            SetVehicleHandlingFloat(vehicle, className, fieldName, decorValue);

                            if (debug)
                                Debug.WriteLine($"{ScriptName}: {fieldName} updated from {value} to {decorValue} for vehicle {vehicle}");
                        }
                    }
                }
                else if (fieldType == FieldType.IntType)
                {
                    if (DecorExistOn(vehicle, fieldName))
                    {
                        var decorValue = DecorGetInt(vehicle, fieldName);
                        var value = GetVehicleHandlingInt(vehicle, className, fieldName);
                        if (value != decorValue)
                        {
                            SetVehicleHandlingInt(vehicle, className, fieldName, decorValue);

                            if (debug)
                                Debug.WriteLine($"{ScriptName}: {fieldName} updated from {value} to {decorValue} for vehicle {vehicle}");
                        }
                    }
                }
                else if (fieldType == FieldType.Vector3Type)
                {
                    string decorX = $"{fieldName}_x";
                    string decorY = $"{fieldName}_y";
                    string decorZ = $"{fieldName}_z";

                    Vector3 value = GetVehicleHandlingVector(vehicle, className, fieldName);
                    Vector3 decorValue = new Vector3(value.X, value.Y, value.Z);

                    if (DecorExistOn(vehicle, decorX))
                        decorValue.X = DecorGetFloat(vehicle, decorX);

                    if (DecorExistOn(vehicle, decorY))
                        decorValue.Y = DecorGetFloat(vehicle, decorY);

                    if (DecorExistOn(vehicle, decorZ))
                        decorValue.Z = DecorGetFloat(vehicle, decorZ);

                    if(!value.Equals(decorValue))
                    {
                        SetVehicleHandlingVector(vehicle, className, fieldName, decorValue);

                        if (debug)
                            Debug.WriteLine($"{ScriptName}: {fieldName} updated from {value} to {decorValue} for vehicle {vehicle}");
                    }
                }
            }
            await Delay(0);
        }

        /// <summary>
        /// Returns true if the <paramref name="vehicle"/> has any handling decorator attached to it.
        /// </summary>
        /// <param name="vehicle"></param>
        /// <returns></returns>
        private bool HasDecorators(int vehicle)
        {
            foreach (var item in handlingInfo.FieldsInfo)
            {
                string fieldName = item.Key;
                Type fieldType = item.Value.Type;

                if (fieldType == FieldType.Vector3Type)
                {
                    if (DecorExistOn(vehicle, $"{fieldName}_x") || DecorExistOn(vehicle, $"{fieldName}_y") || DecorExistOn(vehicle, $"{fieldName}_z"))
                        return true;
                }
                else if (DecorExistOn(vehicle, fieldName))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Registers the decorators for this script
        /// </summary>
        private async void RegisterDecorators()
        {
            foreach (var item in handlingInfo.FieldsInfo)
            {
                string fieldName = item.Key;
                Type type = item.Value.Type;

                if (type == FieldType.FloatType)
                {
                    DecorRegister(fieldName, 1);
                    DecorRegister($"{fieldName}_def", 1);
                }
                else if (type == FieldType.IntType)
                {
                    DecorRegister(fieldName, 3);
                    DecorRegister($"{fieldName}_def", 3);
                }
                else if (type == FieldType.Vector3Type)
                {
                    string decorX = $"{fieldName}_x";
                    string decorY = $"{fieldName}_y";
                    string decorZ = $"{fieldName}_z";

                    DecorRegister(decorX, 1);
                    DecorRegister(decorY, 1);
                    DecorRegister(decorZ, 1);

                    DecorRegister($"{decorX}_def", 1);
                    DecorRegister($"{decorY}_def", 1);
                    DecorRegister($"{decorZ}_def", 1);
                }
            }
            await Delay(0);
        }

        /// <summary>
        /// Remove the handling decorators attached to the <paramref name="vehicle"/>.
        /// </summary>
        /// <param name="vehicle"></param>
        private async void RemoveDecorators(int vehicle)
        {
            foreach (var item in handlingInfo.FieldsInfo)
            {
                string fieldName = item.Key;
                Type fieldType = item.Value.Type;

                if (fieldType == FieldType.IntType || fieldType == FieldType.FloatType)
                {
                    string defDecorName = $"{fieldName}_def";

                    if (DecorExistOn(vehicle, fieldName))
                        DecorRemove(vehicle, fieldName);
                    if (DecorExistOn(vehicle, defDecorName))
                        DecorRemove(vehicle, defDecorName);
                }
                else if (fieldType == FieldType.Vector3Type)
                {
                    string decorX = $"{fieldName}_x";
                    string decorY = $"{fieldName}_y";
                    string decorZ = $"{fieldName}_z";
                    string defDecorX = $"{decorX}_def";
                    string defDecorY = $"{decorY}_def";
                    string defDecorZ = $"{decorZ}_def";

                    if (DecorExistOn(vehicle, decorX)) DecorRemove(vehicle, decorX);
                    if (DecorExistOn(vehicle, decorY)) DecorRemove(vehicle, decorY);
                    if (DecorExistOn(vehicle, decorZ)) DecorRemove(vehicle, decorZ);

                    if (DecorExistOn(vehicle, defDecorX)) DecorRemove(vehicle, defDecorX);
                    if (DecorExistOn(vehicle, defDecorY)) DecorRemove(vehicle, defDecorY);
                    if (DecorExistOn(vehicle, defDecorZ)) DecorRemove(vehicle, defDecorZ);
                }
            }
            await Delay(0);
        }

        /// <summary>
        /// It checks if the <paramref name="vehicle"/> has a decorator named <paramref name="name"/> and updates its value with <paramref name="currentValue"/>, otherwise if <paramref name="currentValue"/> isn't equal to <paramref name="defaultValue"/> it adds the decorator <paramref name="name"/>
        /// </summary>
        /// <param name="vehicle"></param>
        /// <param name="name"></param>
        /// <param name="currentValue"></param>
        /// <param name="defaultValue"></param>
        private async void UpdateFloatDecorator(int vehicle, string name, float currentValue, float defaultValue)
        {
            // Decorator exists but needs to be updated
            if (DecorExistOn(vehicle, name))
            {
                float decorValue = DecorGetFloat(vehicle, name);
                if (Math.Abs(currentValue - decorValue) > 0.001f)
                {
                    DecorSetFloat(vehicle, name, currentValue);
                    if (debug)
                        Debug.WriteLine($"{ScriptName}: Updated decorator {name} updated from {decorValue} to {currentValue} for vehicle {vehicle}");
                }
            }
            else // Decorator doesn't exist, create it if required
            {
                if (Math.Abs(currentValue - defaultValue) > 0.001f)
                {
                    DecorSetFloat(vehicle, name, currentValue);
                    if (debug)
                        Debug.WriteLine($"{ScriptName}: Added decorator {name} with value {currentValue} to vehicle {vehicle}");
                }
            }
            await Delay(0);
        }

        /// <summary>
        /// It checks if the <paramref name="vehicle"/> has a decorator named <paramref name="name"/> and updates its value with <paramref name="currentValue"/>, otherwise if <paramref name="currentValue"/> isn't equal to <paramref name="defaultValue"/> it adds the decorator <paramref name="name"/>
        /// </summary>
        /// <param name="vehicle"></param>
        /// <param name="name"></param>
        /// <param name="currentValue"></param>
        /// <param name="defaultValue"></param>
        private async void UpdateIntDecorator(int vehicle, string name, int currentValue, int defaultValue)
        {
            // Decorator exists but needs to be updated
            if (DecorExistOn(vehicle, name))
            {
                int decorValue = DecorGetInt(vehicle, name);
                if (currentValue != decorValue)
                {
                    DecorSetInt(vehicle, name, currentValue);
                    if (debug)
                        Debug.WriteLine($"{ScriptName}: Updated decorator {name} updated from {decorValue} to {currentValue} for vehicle {vehicle}");
                }
            }
            else // Decorator doesn't exist, create it if required
            {
                if (currentValue != defaultValue)
                {
                    DecorSetInt(vehicle, name, currentValue);
                    if (debug)
                        Debug.WriteLine($"{ScriptName}: Added decorator {name} with value {currentValue} to vehicle {vehicle}");
                }
            }
            await Delay(0);
        }

        /// <summary>
        /// Updates the decorators on the <paramref name="vehicle"/> with updated values from the <paramref name="preset"/>
        /// </summary>
        /// <param name="vehicle"></param>
        private async void UpdateVehicleDecorators(int vehicle, HandlingPreset preset)
        {
            foreach (var item in preset.Fields)
            {
                string fieldName = item.Key;
                Type fieldType = handlingInfo.FieldsInfo[fieldName].Type;
                dynamic fieldValue = item.Value;

                string defDecorName = $"{fieldName}_def";
                dynamic defaultValue = preset.DefaultFields[fieldName];

                if (fieldType == FieldType.FloatType)
                {
                    UpdateFloatDecorator(vehicle, fieldName, fieldValue, defaultValue);
                    UpdateFloatDecorator(vehicle, defDecorName, defaultValue, fieldValue);
                }
                else if(fieldType == FieldType.IntType)
                {
                    UpdateIntDecorator(vehicle, fieldName, fieldValue, defaultValue);
                    UpdateIntDecorator(vehicle, defDecorName, defaultValue, fieldValue);
                }
                else if (fieldType == FieldType.Vector3Type)
                {
                    fieldValue = (Vector3)fieldValue;
                    defaultValue = (Vector3)defaultValue;

                    string decorX = $"{fieldName}_x";
                    string defDecorNameX = $"{decorX}_def";
                    string decorY = $"{fieldName}_y";
                    string defDecorNameY = $"{decorY}_def";
                    string decorZ = $"{fieldName}_z";
                    string defDecorNameZ = $"{decorZ}_def";

                    UpdateFloatDecorator(vehicle, decorX, fieldValue.X, defaultValue.X);
                    UpdateFloatDecorator(vehicle, defDecorNameX, defaultValue.X, fieldValue.X);

                    UpdateFloatDecorator(vehicle, decorY, fieldValue.Y, defaultValue.Y);
                    UpdateFloatDecorator(vehicle, defDecorNameY, defaultValue.Y, fieldValue.Y);

                    UpdateFloatDecorator(vehicle, decorZ, fieldValue.Z, defaultValue.Z);
                    UpdateFloatDecorator(vehicle, defDecorNameZ, defaultValue.Z, fieldValue.Z);
                }
            }
            await Delay(0);
        }

        /// <summary>
        /// Creates a preset for the <paramref name="vehicle"/> to edit it locally
        /// </summary>
        /// <param name="vehicle"></param>
        /// <returns></returns>
        private HandlingPreset CreateHandlingPreset(int vehicle)
        {
            Dictionary<string, dynamic> defaultFields = new Dictionary<string, dynamic>();
            Dictionary<string, dynamic> fields = new Dictionary<string, dynamic>();
            
            foreach(var item in handlingInfo.FieldsInfo)
            {
                string fieldName = item.Key;
                string className = item.Value.ClassName;
                Type fieldType = item.Value.Type;
                string defDecorName = $"{fieldName}_def";

                if (fieldType == FieldType.FloatType)
                {
                    var defaultValue = DecorExistOn(vehicle, defDecorName) ? DecorGetFloat(vehicle, defDecorName) : GetVehicleHandlingFloat(vehicle, className, fieldName);
                    defaultFields[fieldName] = defaultValue;
                    fields[fieldName] = DecorExistOn(vehicle, fieldName) ? DecorGetFloat(vehicle, fieldName) : defaultValue;
                }/*
                else if (fieldType == FieldType.IntType)
                {
                    var defaultValue = DecorExistOn(vehicle, defDecorName) ? DecorGetInt(vehicle, defDecorName) : GetVehicleHandlingInt(vehicle, className, fieldName);
                    defaultFields[fieldName] = defaultValue;
                    fields[fieldName] = DecorExistOn(vehicle, fieldName) ? DecorGetInt(vehicle, fieldName) : defaultValue;
                }*/
                else if (fieldType == FieldType.Vector3Type)
                {
                    Vector3 vec = GetVehicleHandlingVector(vehicle, className, fieldName);

                    string decorX = $"{fieldName}_x";
                    string decorY = $"{fieldName}_y";
                    string decorZ = $"{fieldName}_z";

                    string defDecorNameX = $"{decorX}_def";
                    string defDecorNameY = $"{decorY}_def";
                    string defDecorNameZ = $"{decorZ}_def";

                    if (DecorExistOn(vehicle, defDecorNameX))
                        vec.X = DecorGetFloat(vehicle, defDecorNameX);
                    if (DecorExistOn(vehicle, defDecorNameY))
                        vec.Y = DecorGetFloat(vehicle, defDecorNameY);
                    if (DecorExistOn(vehicle, defDecorNameZ))
                        vec.Z = DecorGetFloat(vehicle, defDecorNameZ);

                    defaultFields[fieldName] = vec;

                    if (DecorExistOn(vehicle, decorX))
                        vec.X = DecorGetFloat(vehicle, decorX);
                    if (DecorExistOn(vehicle, decorY))
                        vec.Y = DecorGetFloat(vehicle, decorY);
                    if (DecorExistOn(vehicle, decorZ))
                        vec.Z = DecorGetFloat(vehicle, decorZ);

                    fields[fieldName] = vec;
                }
            }

            HandlingPreset preset = new HandlingPreset(defaultFields, fields);

            return preset;
        }

        /// <summary>
        /// Prints the values of the decorators used on the <paramref name="vehicle"/>
        /// </summary>
        private async void PrintDecorators(int vehicle)
        {
            if (DoesEntityExist(vehicle))
            {
                int netID = NetworkGetNetworkIdFromEntity(vehicle);
                StringBuilder s = new StringBuilder();
                s.AppendLine($"{ScriptName}: Vehicle:{vehicle} netID:{netID}");
                s.AppendLine("Decorators List:");

                foreach (var item in handlingInfo.FieldsInfo)
                {
                    string fieldName = item.Key;
                    Type fieldType = item.Value.Type;
                    string defDecorName = $"{fieldName}_def";

                    dynamic value = 0, defaultValue = 0;

                    if (fieldType == FieldType.FloatType)
                    {
                        if (DecorExistOn(vehicle, item.Key))
                        {
                            value = DecorGetFloat(vehicle, fieldName);
                            defaultValue = DecorGetFloat(vehicle, defDecorName);
                            s.AppendLine($"{fieldName}: {value}({defaultValue})");
                        }
                    }
                    else if (fieldType == FieldType.IntType)
                    {
                        if (DecorExistOn(vehicle, item.Key))
                        {
                            value = DecorGetInt(vehicle, fieldName);
                            defaultValue = DecorGetInt(vehicle, defDecorName);
                            s.AppendLine($"{fieldName}: {value}({defaultValue})");
                        }
                    }
                    else if (fieldType == FieldType.Vector3Type)
                    {
                        string decorX = $"{fieldName}_x";
                        if (DecorExistOn(vehicle, decorX))
                        {
                            string defDecorNameX = $"{decorX}_def";
                            var x = DecorGetFloat(vehicle, decorX);
                            var defX = DecorGetFloat(vehicle, defDecorNameX);
                            s.AppendLine($"{decorX}: {x}({defX})");
                        }

                        string decorY = $"{fieldName}_y";
                        if (DecorExistOn(vehicle, decorY))
                        {
                            string defDecorNameY = $"{decorY}_def";
                            var y = DecorGetFloat(vehicle, decorY);
                            var defY = DecorGetFloat(vehicle, defDecorNameY);
                            s.AppendLine($"{decorY}: {y}({defY})");
                        }

                        string decorZ = $"{fieldName}_z";
                        if (DecorExistOn(vehicle, decorZ))
                        {
                            string defDecorNameZ = $"{decorZ}_def";
                            var z = DecorGetFloat(vehicle, decorZ);
                            var defZ = DecorGetFloat(vehicle, defDecorNameZ);
                            s.AppendLine($"{decorZ}: {z}({defZ})");
                        }
                        
                    }
                }
                Debug.Write(s.ToString());
            }
            else Debug.WriteLine($"{ScriptName}: Can't find vehicle with handle {vehicle}");

            await Delay(0);
        }

        /// <summary>
        /// Prints the list of vehicles using any decorator for this script.
        /// </summary>
        private async void PrintVehiclesWithDecorators(IEnumerable<int> vehiclesList)
        {
            IEnumerable<int> entities = vehiclesList.Where(entity => HasDecorators(entity));

            Debug.WriteLine($"HANDLING EDITOR: Vehicles with decorators: {entities.Count()}");

            StringBuilder s = new StringBuilder();
            foreach (var vehicle in entities)
            {
                int netID = NetworkGetNetworkIdFromEntity(vehicle);      
                s.AppendLine($"Vehicle:{vehicle} netID:{netID}");
            }
            Debug.WriteLine(s.ToString());

            await Delay(0);
        }

        private string GetXmlFromPreset(string name, HandlingPreset preset)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement handlingItem = doc.CreateElement("Item");
            handlingItem.SetAttribute("type", "CHandlingData");
            handlingItem.SetAttribute("presetName", name);

            foreach (var item in preset.Fields)
            {
                string fieldName = item.Key;
                dynamic fieldValue = item.Value;
                XmlElement field = doc.CreateElement(fieldName);

                Type fieldType = handlingInfo.FieldsInfo[fieldName].Type;
                if(fieldType == FieldType.FloatType)
                {
                    field.SetAttribute("value", ((float)(fieldValue)).ToString());
                }
                else if (fieldType == FieldType.IntType)
                {
                    field.SetAttribute("value", ((int)(fieldValue)).ToString());
                }
                else if (fieldType == FieldType.Vector3Type)
                {
                    field.SetAttribute("x", ((Vector3)(fieldValue)).X.ToString());
                    field.SetAttribute("y", ((Vector3)(fieldValue)).Y.ToString());
                    field.SetAttribute("z", ((Vector3)(fieldValue)).Z.ToString());
                }
                else if (fieldType == FieldType.StringType)
                {
                    field.InnerText = fieldValue;
                }
                else { }
                handlingItem.AppendChild(field);
            }
            doc.AppendChild(handlingItem);

            return doc.OuterXml;
        }

        private async void SavePreset(string name, HandlingPreset preset)
        {
            string kvpName = $"{kvpPrefix}{name}";
            if(GetResourceKvpString(kvpName) != null)
                CitizenFX.Core.UI.Screen.ShowNotification($"The name {name} is already used for another preset.");
            else
            {
                string xml = GetXmlFromPreset(name, preset);;
                SetResourceKvp(kvpName, xml);
                await Delay(0);
            }
        }
        
        private void GetPresetFromXml(XmlNode node, HandlingPreset preset)
        {
            foreach (XmlNode item in node.ChildNodes)
            {
                if (item.NodeType != XmlNodeType.Element)
                    continue;

                string fieldName = item.Name;
                Type fieldType = FieldInfo.GetFieldType(fieldName);

                XmlElement elem = (XmlElement)item;

                if (fieldType == FieldType.FloatType)
                {
                    preset.Fields[fieldName] = float.Parse(elem.GetAttribute("value"));
                }/*
                else if (fieldType == FieldType.IntType)
                {
                    preset.Fields[fieldName] = int.Parse(elem.GetAttribute("value"));
                }*/
                else if (fieldType == FieldType.Vector3Type)
                {
                    float x = float.Parse(elem.GetAttribute("x"));
                    float y = float.Parse(elem.GetAttribute("y"));
                    float z = float.Parse(elem.GetAttribute("z"));
                    preset.Fields[fieldName] = new Vector3(x, y, z);
                }/*
                else if (fieldType == FieldType.StringType)
                {
                    preset.Fields[fieldName] = elem.InnerText;
                }*/
            }
        }

        private void ReadFieldInfo(string filename = "HandlingInfo.xml")
        {
            string strings = null;
            try
            {
                strings = LoadResourceFile(ResourceName, filename);
                handlingInfo.ParseXML(strings);
                var editableFields = handlingInfo.FieldsInfo.Where(a => a.Value.Editable);
                Debug.WriteLine($"{ScriptName}: Loaded {filename}, found {handlingInfo.FieldsInfo.Count} fields info, {editableFields.Count()} editable.");
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                Debug.WriteLine(e.StackTrace);
                Debug.WriteLine($"{ScriptName}: Error loading {filename}");
            }
        }

        private void ReadServerPresets(string filename = "HandlingPresets.xml")
        {
            string strings = null;
            try
            {
                strings = LoadResourceFile(ResourceName, filename);
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(strings);

                foreach (XmlElement node in doc["CHandlingDataMgr"]["HandlingData"].ChildNodes)
                {
                    if (node.NodeType != XmlNodeType.Element)
                        continue;
                    
                    if (node.HasAttribute("presetName"))
                    {
                        string name = node.GetAttribute("presetName");
                        HandlingPreset preset = new HandlingPreset();

                        GetPresetFromXml(node, preset);
                        serverPresets[name] = preset;
                    }
                }
                Debug.WriteLine($"{ScriptName}: Loaded {filename}, found {serverPresets.Count} server presets.");
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                Debug.WriteLine(e.StackTrace);
                Debug.WriteLine($"{ScriptName}: Error loading {filename}");
            }
        }

        private void LoadConfig(string filename = "config.ini")
        {
            string strings = null;
            try
            {
                strings = LoadResourceFile(ResourceName, filename);

                Debug.WriteLine($"{ScriptName}: Loaded settings from {filename}");
            }
            catch (Exception e)
            {
                Debug.WriteLine($"{ScriptName}: Impossible to load {filename}");
                Debug.WriteLine(e.StackTrace);
            }
            finally
            {
                Config config = new Config(strings);

                toggleMenu = config.GetIntValue("toggleMenu", toggleMenu);
                editingFactor = config.GetFloatValue("editingFactor", editingFactor);
                maxSyncDistance = config.GetFloatValue("maxSyncDistance", maxSyncDistance);
                timer = config.GetLongValue("timer", timer);
                debug = config.GetBoolValue("debug", debug);
                screenPosX = config.GetFloatValue("screenPosX", screenPosX);
                screenPosY = config.GetFloatValue("screenPosY", screenPosY);

                Debug.WriteLine($"{ScriptName}: Settings {nameof(timer)}={timer} {nameof(debug)}={debug} {nameof(maxSyncDistance)}={maxSyncDistance}");
            }
        }
    }
}
