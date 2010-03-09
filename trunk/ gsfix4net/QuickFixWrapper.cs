using System;
using System.Collections.Generic;
using System.Text;
using QuickFix;
using System.Collections;
using System.Windows.Forms;


namespace QuickFixInitiator
{
    class QuickFixWrapper : QuickFix.MessageCracker, QuickFix.Application
    {
        private string settingfile = "initiator.cfg";
        private QuickFix.SessionID _ssnid;
        private QuickFix.SocketInitiator _socketInitiator;//可使用ThreadedSocketInitiator
        private QuickFix.FileStoreFactory _messageStoreFactory;//可使用文件、内存、数据库
        private QuickFix.SessionSettings _settings;
        private QuickFix.FileLogFactory _logFactory;//可使用屏幕、数据库、文件
        private QuickFix42.MessageFactory _messageFactory;
        private Hashtable fCancelRequests;
        private GSFIXConnectionOption option;
        public event EventHandler OnError;
        public event EventHandler OnConnection;
        public event EventHandler OnDisconnection;
        public event OrderCancelRejectEventHandler OrderCancelReject;
        public event ExecutionReportEventHandler ExecutionReport;
        public QuickFixWrapper()
        {
            Init();
            this.fCancelRequests = new Hashtable();
        }
    
        public void onCreate(QuickFix.SessionID pSessionID) 
        {
            _ssnid = pSessionID;
        }
        public void onLogon(QuickFix.SessionID pSessionID) 
        {
            if (OnConnection != null)
            {
                OnConnection(this, EventArgs.Empty);
            }
        }
        public void onLogout(QuickFix.SessionID pSessionID) 
        {
            if (OnDisconnection != null)
            {
                OnDisconnection(this, EventArgs.Empty);
            }
        }
        /// <summary>
        /// 发送给对端的会话层报文，通过CONSOLE来输出
        /// </summary>
        /// <param name="message"></param>
        /// <param name="pSessionID"></param>
        public void toAdmin(QuickFix.Message message, QuickFix.SessionID pSessionID)
        {
            if (message is QuickFix42 .Logon )//确认是登陆请求
            {
                string rawdata = option .Rawdata ;
                message.setString(96, rawdata);
            }
            Console.WriteLine("Enter toAdmin");
            Console.WriteLine(message.ToString());
            Console.WriteLine("Exit toAdmin");
          //  MessageBox.Show("toAdmin"+message .ToString ());  //20091229 
        }
        /// <summary>
        /// 发送给对端的应用层报文，需要业务逻辑处理
        /// </summary>
        /// <param name="pMessage"></param>
        /// <param name="pSessionID"></param>
        public void toApp(QuickFix.Message pMessage, QuickFix.SessionID pSessionID) 
        { 
            //可以自行做业务逻辑，例如转发、LOG等
            MessageBox.Show("toApp "+pMessage .ToString ());
        } 
        /// <summary>
        /// 对端发来的会话层报文，通过CONSOLE来输出
        /// </summary>
        /// <param name="pMessage"></param>
        /// <param name="pSessionID"></param>
        public void fromAdmin(QuickFix.Message pMessage, QuickFix.SessionID pSessionID) 
        {
            Console.WriteLine("Enter fromAdmin");
            Console.WriteLine(pMessage.ToString());
            //MessageBox.Show("fromAdmin 动作"+pMessage .ToString ());  //20091229
            Console.WriteLine("Exit fromAdmin");
        }    
        /// <summary>
        /// 来自对端的应用层报文，需要业务逻辑处理
        /// </summary>
        /// <param name="pMessage"></param>
        /// <param name="pSessionID"></param>
        public void fromApp(QuickFix.Message pMessage, QuickFix.SessionID pSessionID) 
        {
            base.crack(pMessage, pSessionID);//调用默认处理方法即可
            MessageBox.Show("" + pMessage.ToString());
            pMessage.Dispose();//清理现场，重要
        }

        public void Connect(GSFIXConnectionOption o)
        {
            if (o.Rawdata == "")
            {
                if (OnError != null)
                {
                    OnError("未正确设置登陆验证信息", EventArgs.Empty);
                }
            }
            else
            {
                option = o;
                this._socketInitiator.start();
                Session session = Session.lookupSession(_ssnid);
                if ((session != null) && !session.isLoggedOn())
                {
                    session.logon();
                }
            }
        }
        public void Disconnect()
        {
            Session session = Session.lookupSession(_ssnid);
            if ((session != null) && session.isLoggedOn())
            {
                session.logout();
            }
            //this._socketInitiator.stop();//必须注释这条语句，否则无法清理现场
        }
        public void Send(QuickFix42.NewOrderSingle message)
        {
            Session.sendToTarget(message, _ssnid);
        }
        public void Send(QuickFix42.OrderCancelRequest message)
        {
            string ClOrdID = message.getClOrdID().getValue();
            if (!fCancelRequests.ContainsKey(ClOrdID))
            {
                this.fCancelRequests.Add(ClOrdID, message);
                Session.sendToTarget(message, _ssnid);
            }
            else
            {
                Console.WriteLine("Order " + ClOrdID + " already pending cancel process");
            }
        }
        public override void onMessage(QuickFix42.ExecutionReport report, SessionID sessionID)
        {
            if (ExecutionReport != null)
            {
                ExecutionReport(sessionID, new ExecutionReportEventArgs(report));
            }  
        }
        public override void onMessage(QuickFix42.OrderCancelReject message, SessionID SessionID)
        {
            if (OrderCancelReject != null)
            {
                OrderCancelReject(SessionID, new OrderCancelRejectEventArgs(message));
            }    
        }

        private void Init()
        {
            try
            {
                this._settings = new QuickFix.SessionSettings(settingfile);
                this._messageStoreFactory = new QuickFix.FileStoreFactory(this._settings);
                this._logFactory = new QuickFix.FileLogFactory(this._settings);
                this._messageFactory = new QuickFix42.MessageFactory();
                this._socketInitiator = new QuickFix.SocketInitiator  (this, _messageStoreFactory, this._settings, this._logFactory, _messageFactory);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }

    //在登录消息的RawData域送入柜台登录所需的相关字段,格式为:
    //登录类型:登录标识:交易密码:
    // n  登录类型:
    //‘B’表示以银行帐户登录  -登录标识为银行帐户
    //‘Z’表示以资金帐户登录  -登录标识为资金帐户
    //‘C’表示以客户代码登录  -登录标识为客户代码（含代理人代码）
    //‘K’表示以磁卡登录      -登录标识为磁卡号 
    //‘X’表示代理人登录      -登录标识为代理人
    //‘N’表示用股东内码登录  -登录标识为股东内码
    //其他表示以股东代码登录  -登录标识为对应市场的股东代码（代码未实现该功能） 
    //
    //详细信息可参考《国信证券外围交易接口说明V3.05》用户登录(420301)接口
    public class GSFIXConnectionOption
    {
        private string rawdata;
        public GSFIXConnectionOption(string logintype,string id, string pwd)
        {
            switch (logintype)
            {
                case "B":
                case "Z":
                case "C":
                case "K":
                case "X":
                case "N":
                    rawdata = logintype + ":" + id + ":" + pwd;
                    break;
                default :
                    rawdata = "";
                    break;
            }
        }
        public string Rawdata
        {
            get
            {
                return rawdata;
            }
        }
    }
    public class ExecutionReportEventArgs : EventArgs
    {
        // Fields
        private QuickFix42.ExecutionReport report;

        // Methods
        public ExecutionReportEventArgs(QuickFix42.ExecutionReport report)
        {
            this.report = report;
        }

        // Properties
        public QuickFix42.ExecutionReport ExecutionReport
        {
            get
            {
                return this.report;
            }
        }
    }
    public delegate void ExecutionReportEventHandler(object sender, ExecutionReportEventArgs args);
    public class OrderCancelRejectEventArgs : EventArgs
    {
        // Fields
        private QuickFix42.OrderCancelReject reject;

        // Methods
        public OrderCancelRejectEventArgs(QuickFix42.OrderCancelReject reject)
        {
            this.reject = reject;
        }

        // Properties
        public QuickFix42.OrderCancelReject OrderCancelReject
        {
            get
            {
                return this.reject;
            }
        }
    }
    public delegate void OrderCancelRejectEventHandler(object sender, OrderCancelRejectEventArgs args);
}
     
