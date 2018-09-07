﻿using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using KatLib;
using SimpleJSON;


namespace KXAPI
{
    [KSPAddon(KSPAddon.Startup.EveryScene, false)]
    public class KerbalXLoginUIHelper : MonoBehaviour
    {
        internal static KerbalXLoginUIHelper instance = null;

        private void Awake(){
            if(instance != null){
                GameObject.Destroy(instance);
            }
            instance = this;
        }

        internal void start_login_ui(){
            KerbalXAPI.log("start_login_ui, called on LoginUIHelper");
            KXAPI.login_ui = gameObject.AddOrGetComponent<KerbalXLoginUI>();
        }
    }

//    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class KerbalXLoginUI : DryUI
    {
        internal KerbalXAPI api = new KerbalXAPI ("KerbalXAPI", KXAPI.version);
        internal string username = "";
        internal string password = "";
        internal bool enable_login     = true; //used to toggle enabled/disabled state on login fields and button
        internal bool login_failed     = false;//if true, displays login failed message and link to recover password on the site
        internal bool login_successful = false;//if true, hides login field and shows logged in as and a logout button
        internal bool modal_dialog = false;
        internal string login_required_message = "";
        internal bool show_cancel = false;
        internal bool initial_token_check_complete = false;
        internal GUIStyle login_indicator = null;
        private bool dialog_open = false;
        private bool window_retract = true;

        private float window_out_pos = -15f;
        private float window_in_pos = -420f;

        private int count = 5;


        internal static Dictionary<string,AfterLoginCallback> after_login_callbacks = new Dictionary<string, AfterLoginCallback>();

        internal static void open_login_ui(){
            KerbalXLoginUIHelper.instance.start_login_ui();
        }

        internal void login(){
            login(username, password);
        }
        internal void login(string username, string password){
            KerbalXAPI.log("logging in....");
            enable_login = false; //disable interface while logging in to prevent multiple login clicks
            login_failed = false;
            login_indicator = null;
            api.login(username, password, (resp, code) =>{
                if(code == 200){
                    var resp_data = JSON.Parse(resp);
                    KerbalXAPI.log("Logged in");
                    login_successful = true;
                    process_callbacks(true);
                    show_upgrade_available_message(resp_data["update_available"]); //triggers display of update available message if the passed string is not empty
                } else{
                    KerbalXAPI.log("NOT Logged in");
                    login_failed = true;
                    enable_login = true;
                }
                enable_login = true;
                autoheight();
                password = "";
            });
        }

        //Check if Token file exists and if so authenticate it with KerbalX. Otherwise instruct login window to display login fields.
        internal void load_and_authenticate_token(){
            KerbalXAPI.log("logging in....");
            enable_login = false;
            login_indicator = null;
            api.login((resp, code) =>{
                if(code == 200){                    
                    var resp_data = JSON.Parse(resp);
                    KerbalXAPI.log("Logged in");
                    process_callbacks(true);
                    show_upgrade_available_message(resp_data["update_available"]); //triggers display of update available message if the passed string is not empty
                }else{
                    KerbalXAPI.log("NOT Logged in");
                }
                enable_login = true;
                initial_token_check_complete = true;
                autoheight();
            });
        }

        internal void logout(){
            api.logout((resp, code) =>{
                enable_login = true;
                login_successful = false;
                username = "";
                password = "";
                KerbalXAPI.log("Logged out of KerbalX");
            });
        }

        internal void process_callbacks(bool login_successful){
            List<string> callback_keys = new List<string>();
            foreach(string key in after_login_callbacks.Keys){
                callback_keys.Add(key);
            }
            foreach(string key in callback_keys){
                after_login_callbacks[key](login_successful);
                after_login_callbacks.Remove(key);
            }
        }

        protected override void OnGUI(){
            //Trigger the creation of custom Skin (copy of default skin with various custom styles added to it, see stylesheet.cs)
            if(KXAPI.skin == null){
                KXAPI.skin = new StyleSheet(HighLogic.Skin).skin;
                KXAPI.alt_skin = new StyleSheet(GUI.skin).skin; //works but isn't as clear.
            }
            if(this.skin == null){
                this.skin = KXAPI.skin;
            }
            GUI.skin = skin;
            base.OnGUI();
            GUI.skin = null;
        }


        private void Start(){
            window_title = null;
            window_pos = new Rect(window_in_pos, 50, 420, 5);
            KXAPI.login_ui = this;

            if(RequestHandler.instance == null){
                KerbalXAPI.log("starting web request handler");
                RequestHandler request_handler = gameObject.AddOrGetComponent<RequestHandler>();
                RequestHandler.instance = request_handler;
            }

            if(api.logged_out){
                //try to load a token from file and if present authenticate it with KerbalX.  if token isn't present or token authentication fails then show login fields.
                load_and_authenticate_token();   
            }

//            enable_request_handler();
//            if(KerbalX.enabled){                
//                enable_request_handler(); //TODO why is  this here twice (fix when sober)
//            } else{
//                GameObject.Destroy(CraftManager.login_ui);
//            }
        }

        protected override void WindowContent(int win_id) {            
            if(modal_dialog){                
                if(!dialog_open){
                    dialog_open = true;
                    ModalDialog dialog = gameObject.AddOrGetComponent<ModalDialog>();
                    dialog.dialog_pos = new Rect(Screen.width / 2 - 450f / 2, Screen.height / 3, 450f, 5f);
                    dialog.window_title = window_title;
                    dialog.content = new DialogContent(d =>{
                        login_content(450f);                    
                    });
                    dialog.skin = KXAPI.skin;
                }
            } else{                
                section(400f, 5f, "login.container", (inner_width) =>{
                    alt_window_style = skin.GetStyle("login.window");                    
                    GUILayout.BeginVertical("Window", width(400f), height(100f), GUILayout.ExpandHeight(true));
                    login_content(400f);
                    GUILayout.EndVertical();
                    v_section(20f, w =>{
                        fspace();
                        if(login_indicator == null || !enable_login){
                            login_indicator = "login.logging_in";
                        } else if(api.logged_in){                            
                            login_indicator = "login.logged_in";
                        }else if(api.logged_out){
                            login_indicator = "login.logged_out";
                        }
                        label("K\ne\nr\nb\na\nl\nX", "centered", 10f);
                        label("", login_indicator);
                    }, (evt) => {
                        if(evt.single_click){
                            if(window_pos.x < window_out_pos){
                                window_retract = false;
                            }else if(window_pos.x >= window_out_pos){
                                window_retract = true;
                            }
                            initial_token_check_complete = false;
                        }
                    });
                });

                if(initial_token_check_complete && api.logged_out){
                    window_retract = false;
                }
                if(window_retract && window_pos.x > window_in_pos){
                    window_pos.x -= 10;
                } else if(!window_retract && window_pos.x < window_out_pos){
                    window_pos.x += 10;
                }
            }
        }


        protected void login_content(float content_width){
            if(!modal_dialog){
                skin = KXAPI.alt_skin;
            }
            section(content_width, 110f, () =>{
                v_section(content_width, (inner_width) =>{

                    if(!String.IsNullOrEmpty(login_required_message)){
                        label(login_required_message, "h2");
                    }

                    if (api.logged_out) {                  
                        gui_state(enable_login, () =>{                    
                            label("CraftManager - KerbalX.com login");
                            section(() => {
                                label("username", width(70f));
                                GUI.SetNextControlName("username_field");
                                username = GUILayout.TextField(username, 255, width(inner_width-85f));
                            });
                            section(() => {
                                label("password", width(70f));
                                password = GUILayout.PasswordField(password, '*', 255, width(inner_width-85f));
                            });
                            Event e = Event.current;
                            if (e.type == EventType.keyDown && e.keyCode == KeyCode.Return && !String.IsNullOrEmpty(username) && !String.IsNullOrEmpty(password)) {
                                login(username, password);
                            }
                        });
                        if(!enable_login){
                            label("Logging in....", "h2");
                        }
                    }else if (api.logged_in) {
                        label("CraftManager has logged you into KerbalX.com");
                        label("Welcome back " + api.logged_in_as);
                    }
                    if (login_successful) {
                        section(() => {
                            label("KerbalX.key saved in KSP root", width(inner_width - 50f));
                            button("?", 20f, ()=>{
                                DryDialog dialog = show_dialog(post_login_message);
                                dialog.window_title = "KerbalX Token File";
                                dialog.window_pos = new Rect(window_pos.x + window_pos.width + 10, window_pos.y, 350f, 5);                        
                            });
                        });
                    }

                    section((w)=>{                        
                        if (api.logged_out) {                
                            gui_state(enable_login, () =>{
                                button("Login", login);
                            });
                        } else {
                            button("Logout", logout);
                        }
                        if(show_cancel){
                            button("Cancel", ()=>{
                                process_callbacks(false);
                                close_dialog();
                                GameObject.Destroy(KXAPI.login_ui);
                            });
                        }
                    });

                    GUI.enabled = true; //just in case

                    if (login_failed) {
                        v_section(() => {
                            label("Login failed, check your things", "alert");
                            button("Forgot your password? Go to KerbalX to reset it.", ()=>{
                                Application.OpenURL("https://kerbalx.com/users/password/new");                        
                            });
                        });
                    }
                });
            });

            if(count >= 0){
                GUI.FocusControl("username_field");
                count -= 1;
            } 
        }

        private void post_login_message(DryUI d){
            string message = "The KerbalX.key is a token that is used to authenticate you with the site." +
                "\nIt will also persist your login, so next time you start KSP you won't need to login again." +
                "\nIf you want to login to KerbalX from multiple KSP installs, copy the KerbalX.key file into each install.";
            label(message);
            button("OK", close_dialog);
        }

        //Shows an upgrade available message after login if the server provides a upload available message string
        internal void show_upgrade_available_message(string message) {
            if (!String.IsNullOrEmpty(message)) {

                DryDialog dialog = show_dialog((d) => {
                    v_section(w => {
                        label("A new version of CraftManager is available");
                        label(message);
                        section("dialog.section", ()=>{
                            button("visit KerbalX to download the latest version", "hyperlink", ()=>{
                                Application.OpenURL(api.url_to("mod"));
                            });                            
                        });
                        section(w2 => {                           
                            button("Remind me later", close_dialog);
                            button("Don't notify me about this update", ()=>{
                                api.dismiss_current_update_notification();
                                close_dialog();

                            });
                        });

                    });
                });
                dialog.window_title = "CraftManager - Update Available";
                dialog.window_pos = new Rect(window_pos.x + window_pos.width + 10, window_pos.y, 400f, 5);
            }
        }
    }
}
