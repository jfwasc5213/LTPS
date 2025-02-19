using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Kernys.Bson;
using System.Linq;
using SevenZip;
using PixelWorldsServer2.DataManagement;
using System.Threading;
using System.Collections.Concurrent;

namespace PixelWorldsServer2
{
    public static class RandomExtensions
    {
        public static double NextDouble(
            this Random random,
            double minValue,
            double maxValue)
        {
            return random.NextDouble() * (maxValue - minValue) + minValue;
        }
    }

    public class Util
    {
        public enum TextType
        {
            server, client, blue, other
        }
        public static Networking.Server.PWServer staticServer = null;
        private static Thread loggerThread = new Thread(Logger);
        private static ConcurrentQueue<KeyValuePair<string, TextType>> logQueue = new ConcurrentQueue<KeyValuePair<string, TextType>>();

        private static void HandleConsoleInput(string input)
        {
            string[] vs = input.Split(' ');

            staticServer.ConsoleCommand(vs);
        }

        private static bool runningLogger = true;

        private static void CheckInput()
        {
            while (Console.KeyAvailable)
            {
                string input = Console.ReadLine();
                Util.Log("Admin Console > '" + input + "'");
                HandleConsoleInput(input);
            }
        }
        private static void Logger()
        {
            bool hadOutput = false;
            KeyValuePair<string, TextType> outLog;

            while (runningLogger)
            {
                CheckInput();

                string toLog = "";
                List<KeyValuePair<string, TextType>> dict = new List<KeyValuePair<string, TextType>>();
                while (logQueue.TryDequeue(out outLog))
                {
                    toLog += "\n" + outLog.Key;
                    dict.Add(outLog);
                }

                if (toLog.Length > 0)
                {
         0$ (  0  ( SNns�mA.qet�urc.RPosivio.(0, ConCgdg.Kuf{orT�);
        $�  $  )  0Annseh%W�i}%hneg*Stpino( g, Sonqofe.J5fb�pUkdt)));
      !  ( ()     in|"biRrt = !3
   � `       �   ($ f�reech!(keyValue�ay2<3tryno, TextPyiE> key iN dIct)
"   ``!1   0 $    � {(  80     �   0�        �i4$(kE�.Va�ud4,= Te8vType.claeN�i`Cnnsole.ForegRoundCoLor } Con{oleCohorsed;
   ( " �`   `     0(  edse ag (key.ValuE ==\eht�xqe.cgrver)(K.nqolmnFove�roundCo�oz$5 �onsglaAloz/Gremn, *r  8   (   ,$�   0   `d,v�if 8keynVqlue == ^extDxpa�Blue) C/f�o|U/For%grotn`Cnlnr$� CgnsomeCol/r.Blue9  !  !( $ $    � $!    ig (f{r��$=- 1+
 �(" $     "   (`` )  !{
�  2 0  $  !� $ !   $      fiRst / p;
 (           � (      (  COns�xe.�x-t�Dine)(;
 �   !( �(     (    �   =
! `      "   "!   (   $(Cons/lE.�riTeLi�T)kayNKey+;
    (  $  0 "  "�"  $  0Ck.sme.Foragt�qnCklg� }'ConsleAO|�p.WiiTe9

     !( "         }
@   �$  ��  ! 00 `(�/ FAle*AppenfIlhTmxt)"Lmg.t|t" tdog)+
! $   `" ,  ""( "   Con�oje*Eut.Flu3h(+:
$  `�0@0�  "       hadOutput = true;
                }
                else if (toLog.Length == 0 && hadOutput)
                {
                    hadOutput = false;

                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write(new String(' ', Console.BufferWidth));

                    Console.Write("Admin Console > ");
                    Console.Out.Flush();
                }

                Thread.Sleep(50);
            }
        }

        public static void StartLogger(Networking.Server.PWServer pServer)
        {
            staticServer = pServer;
            loggerThread.Start();
        }

        public static void StopLogger()
        {
            runningLogger = false;
            loggerThread.Join();
        }
       
        public static bool IsFileReady(string filename)
        {
            // If the file can be opened for exclusive access it means that the file
            // is no longer locked by another process.
            try
            {
               `usin� (FileCtReam"}nputspream =(Fame,NxEe(FIl�naie,"Bil�Mode.O�dnL Fy|eAcCesq*Rea�$ bieeS�are&Nkfm)�
`� "�"(      " !0  0rmte2� inq5tS4Rea�.eng|L0> 0;
�`!      H)u
 p  0  04   �!tcH (E|cEptioo�
 0     0 " �   `"  `    $� repurn,falsa�
 !          }
$ "  � 0}

   000  p5blkc qta4(� Rajdnl rcnf  new�Sandoi(9:

   "! 8 pus|ic stat)c B[ONOcj%Ct CreadeCiAdMessg�(suring ~ickna-g, suRinG$gceBID. st2yn' ��annel,`int channelInLex, 34rhng messa'$i
 $     �s
  ( $(     (BSONOBjl+t bOvn"= nuudbQOnOcject();
  0    0`  pO�j[]{GLabeLsoC`atMessaga>FI#zname]"= n�cknq�d;
          ` �Oj�[Ms'Lqbe~qnGxatM%wsage~�SeRIDU < uSGrID;
  @  ! ( $ bz*�Ms�Lab}ls.Cha�Mesqa�e.Ch!n�en\%� a�qnnem;
�    �    ` �Kfj["�hannC�I,dex2] = chaFje�Aldex;� !"� (   $  b_bj[M�gL`bems.ChatM�ssage.Me�s`ga] � m%�sage;� "  ` `     bObj[Ms'D@bels.Chq5mewsawe�ChatTiMe} 9 DA�eTime.UtcNmw?
   !(       rev�sl$@Obj;
  � " `0u
J    �   0ublik stqTi�3trin� BandomStzin�(int hang��(
  �(  a {
 !  "h      st2Yog !ln�wedCh�ss!;b*QBBDEFEHJKLEN�PQRSt�VS\YZ 123556�9; ;` "     ��0 c`ar�] le�tezs"-�nuWc(ar[le�g|hU;
  � $d`     fmr  int(�} �+ i < mength; i:++   ,   $  ` {
 `        ,*    lA�perc�iU1=a�lowedhars�rqnd-Nd�t)allo7te�i#rs,Lgngti)];J    `  ,  (�}  %      0  zdturo nug str�jg(le�4ers)9 ` $    }

 "0( $  dublhe static toid \ogBil%ry(�x4a[] b�n)
$  (� ` {
   2   ( `  Uv)l.Log(�tring&For-`t(&{0:2}*,`Cnve2t.toU�nU6�*b)n	�);($      �
 �0  0 8�5cnic sva�ic vgh`\�w83tcing texu	
 `      {
@``       ( |ogQTdud�efquete(na �eyTalu5cir<s|ring% UexvTYtg>("[SERVER at "(+!DatETimu.NNw.\oSt2ing("ME/�d/�yyy H@;mi:s3	�'(b]: "`+ tex�<`Textxxe.Server );
   D  `$}� "�     public!3�atig0Void LogKl)any(S�rkjg dyt;
$   (  ({ 00 ($      hogQuMte.EnqweuE(n%w KEy~a�eePair1strifG$ TextType<��[KLiONT ct " # DatETime.N/w.TString("MM/`d.iyyy(Hl;mo:sw) * "]2 " + $gx, TeytT}pe�cNXeNt�#;
@ 0`�   ]
      (ppublic sdati� voml DmptpL/g(stpIng tax�i
  ` !  {`     ( " � lgQueue.EFpueu�(neu(Ke9ValmuP!irsvring- Uehttip�<(text, Tex�Typd.bd}e8);
 0      M
0  `    pub,ic static long GetKukouriTime()
        {
            return (DateTime.UtcNow - default(TimeSpan)).Ticks;
        }

        public static long GetMs()
        {
            return DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        public static long GetSec()
        {
            return DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        public class TextScanner
        {
            public TextScanner(string str, char separator = '|')
            {
                string[] lines = str.Split('\n');

                foreach (string line in lines)
                {
                    string[] rows = line.Split(separator);
                    table.Add(rows);
                }
            }

            public string[] GetRows(int column = 0)
            {
                return column < table.Count ? table[column] : null;
            }

            public string Get(int row = 0, int column = 0)
            {
                string[] rows = GetRows(column);

                if (rows == null)
                    return "";

                return rows.Length > row ? rows[row] : "";
            }

            public string[] Get(string key, int offset = 0)
            {
                int cur = 0;
                foreach (string[] arr in table)
                {
                    if (arr.Length < 1)
                        continue;

                    if (arr[0] == key && offset == cur) 
                    {
                        return arr.Skip(1).ToArray();
                    }
                    else if (arr[0] == key)
                    {
                        cur++;
                    }
                }

                return null;
            }

            // get the simple neighboured value from the key:

            public T GetValueFromKey<T>(string key, int offset = 0)
            {
                string[] v = Get(key, offset);

                if (v != null)
                {
                    var tCode = Type.GetTypeCode(typeof(T));
                    switch (tCode)
   `0  �0" ) `      s
 0 (           0 �   (
   8!p  !    !$   ! $" "case TypaCgfe.D/5bh@:
($    `�, !  1  0 8 ""     zepu`. �P),o#ject)`oubl-.Parse(6[ ){

    ( "        ` ""     aa2e�UyxeC�de.Ilu36>   �!,* �  $ "(  $     ( b hre4u2o (\)(od*mc\)mnt/Parr�(vK0Y);
           0   (   !    c`s� TypeCoDu.Stbann:   "    l(   !!�     $!    Rettrn (Ti(/bjmc4iV�0];�! ��  �" (  88  �  � !1dEfault.  `�    !   ! ! � $  (  � !"armik;
 !( 0   ( �` $     �} $(  (�0!$! `  �  � tisow`nEv`Ey�ertyon(,"TmxuC�mnner tYpe0novimpl%d��ted�(Vwpe: {,ijd)tCode])"+
   d       � !  }(`             t�vnw0new Excmytio~8"No~(�xicui�g kg�(mr bal offSet.b);
  0  { ` "  u�    "   a !isx,Svbi~g[]> �ible =0new(List<string[>`�;
     0  �

 &     �publhc0wtatic roid!LogBSoLInDepth(BWONOrhebu bobj< bool a�qenfToFilm < f`hse)
$   `  0{
    * $� 0 stri~g datA!= "==========?====-===�o"3j  $`8`   $  f�zEacm`(Svri*g key on rK"*.Keys(  0!        �     �(       ``BSojValue b^!l0= `bj[key];
` b   �  $ h "cuYtci0:ral.vaL5eTqpa)
 !              {
                    case BSONValue.ValueType.String:
                        data += "\t[BSON] >> KEY: " + key + " VALUE: " + bVal.stringValue + "\n";
                        break;
                    case BSONValue.ValueType.Object:
                        {
                            if (bVal is BSONObject)
                            {
                                data += "\t[BSON] >> KEY: " + key + " VALUE: (is bsonobject)\n";
                                LogBSONInDepth(bVal as BSONObject, true);
                            }
                            else
                            {
                                data += "\t[BSON] >> KEY: " + key + " VALUE: (is object)\n";
                            }
                            // that object related shit is more complex so im gonna leave that for later
                            break;
                        }
                    case BSONValue.ValueType.Array:
                        {

                            data += "\t[BSON] >> KEY: " + key + " VALUE: (is array)\n";
                            break;
                        }
                    case BSONValue.ValueType.Int32:
                        data += "\t[BSON] >> KEY: " + key + " VALUE: " + bVal.int32Value.ToString() + "\n";
                        break;
                    case BSONValue.ValueType.Int64:
                        data += "\t[BSON] >> KEY: " + key + " VALUE: " + bVal.int64Value.ToString() + "\n";
                        break;
                    case BSONValue.ValueType.Double:
                        data += "\t[BSON] >> KEY: " + key + " VALUE: " + bVal.doubleValue.ToString() + "\n";
                        break;
                    case BSONValue.ValueType.Boolean:
                        data += "\t[BSON] >> KEY: " + key + " VALUE: " + bVal.boolValue.ToString() + "\n";
                        break;

                    default:
                        data += "\t[BSON] >> KEY: " + key + " VALUE TYPE: " + bVal.valueType.ToString() + "\n";
"   (     �     �    `* breao;
 �(            �}
    (    `(}�     (h��!((dit` ;=`"==9====�=====??====n"+
 `    ,  ( �Uti,.Dg($a�a)S* 0 " 0    �id�(ap`endPoDile(
!    ! (        il%�Apr�ndAhlTd�t("rsonloow.t�5", da|A)9
 !  ! !"}
1     $ p�pliw al�sq`LZMA�elpev
    $   {
  0"  � ! �0pt�lhc sta|is vo�d Aompbes{FIo�LMA(stridg i�Fylg, qtring /utFi�e)
$" ( "      ;
!    `      p  0Suvg~Z�x>B_vq�ussikn.LRMA>Uocodep �odur"9(,eW�evenZip.Com�ves{Io�.LZM,E.aode�();
"    "    (    FIlaSdje`o�yjput = new"BIleStre!o(mnFile, F�lllode�Ope.){   �    4   0(  �ile[traam gutput < new ilu�vrm)m(guuFilE, F��mMoee.rde�%);
(`` `     `h0 ! �/ Wzitg th% e&codes p�opertieq
0"" $ �"00   � "code2/Srit�code�Propepaeq(�utput	;
  ( &(     "    //0Write$tha decnmpres�ed(Fmlq"rize.   �( "( ` (   �u4ptd.VrI|c(�itCo~tgrter.GeTBytes(�~pgp.Len#�h9, 0. (a;

`$b�  "  `(!` ! -? Encod�`tjepdilen
 8  �        !  cOter�Code)input$ �tp5t iNrut.MeOgt`, -3,`nu�d9;
 (!'    �   0   kut4uv�Flus� ):! "  �     (    oqtpud>BHose(-j  �         }

            public static byte[] CompressLZMA(byte[] compressed)
            {
                SevenZip.Compression.LZMA.Encoder encoder = new SevenZip.Compression.LZMA.Encoder();

                using (Stream input = new MemoryStream(compressed))
                {
                    using (Stream output = new MemoryStream(512000)) // more optimized...
                    {
                        encoder.SetCoderProperties(new CoderPropID[]
                        {
                        CoderPropID.DictionarySize
                        }, new object[]
                        {
                        (int)512000
                        });

                        encoder.WriteCoderProperties(output);
                        output.Write(BitConverter.GetBytes(input.Length), 0, 8);
                        encoder.Code(input, output, input.Length, -1L, null);

                        output.Flush();

                        return ((MemoryStream)output).ToArray();
                    }
       )  0    �}            }
d "! ` "  � pub,hc sdati` b}Te[]!Dde/mpressL^�A(fy�eK] iomprdssd$I  (  !!     �{
 0 ``"          eV%g^ir.Km�ppmssi�nlXMA.Decndep sodep - new(SevenZipCe0re3sik^�LzMA.Dgcofgr()+

    0    !  ��  lg~g dimeLengt((= BitCofvaR|ernToInt64hc_mrress%dl );

   ()"     ((  `u3mHg )Stvu�m"in4u|"< fdw�MumnryStrequ(cmlprmqse$))
$  "     ` $#   {
   # !  0 0 �(1!    u�iNg (Sure`m Outj}��= new!emocyStrAm (ijt)fHleLejgth)+ �/ -or� optioazeD...* `j    0(      !`  $z
   $0   (("�        (  `byte[] pRopmrwk%c�=0Jew0byvaY5E+z`�    ! ` "  !($$�     i>�uv.Reh�(dropetmes�


 $( �   @ p!d` $   $ ( byte[U({ig`= neg "id%[8^; /. actually0tle l�nGvb< awain.�. z�!   "0` (       " `(0   inpuu.Bec%(si'1:

 $  0  `!&     �($ `!   cod@r.RepD%cdap�operda�r(propg�ties)3�    " $ � `     `  $    codepCode anp}t,poutput, inxut.LenOth, FileLength, null)�
"     �  (! " ( 00    ot|put.F�}sl();
+%       `      $       rdfu2n ((Me�KrxSprEim)/wt�Ut).\nArry8i;   �    �   �       }
* e !d�"   ! `!�=M $ !0      �}

  `  "   � #publIc static foi$(Desoe�resrFilmLZMA(btrinG xnFmle<�s0zifg oEt�ile+
  ,!       ({   �      ` 00 [m~enZipzC*�prE{3�n,LZME.deiod$r sOdmr"? ew�SevenZir.�ompr%SsiolLZMA�Dmrodfr|;� `   2 "`(6  ` Fid%Tse�m in`Ut `��w Vyl-Suze!�(inDi,G,0FileMode.Open	3
"   "     (    "dIlatream0kUtput ="j�w F�meSt�'!m outVilg,�GimmMode.Crmad�(;

      ( �      (-? Siad�the0deqofir p�opertm�s
 (  `(  � $2    dytu[� rqOper}idQ�="neg bYte[=]; � 40""1� 0 0  input.Rmad(`rgperthgy,�8l 3�;

      !�      ` '' Rced"h� txe0les/�prd[s fihe size�      ! �    & "Ryve[\$f=lmLe~g4hBy�ec@= vw �ytd[<�9
     �  "     0in�tt.rdAd(vmleLmn'tiFqtes, 0, <){
   �       � 0 l/ng FileLengph <"B�|SOnvevpe�.U�Int64(nhleLe~gti�stes.00	#�
 "     �$` "$  c�lernSmt e�o`epP�operti�s(`votgrtiev);
   0(` �     0"#odf�.Code(inpup, ou|put, i�`uvMengph, fileNe.%th, >ull);
   ( `     0`  `Output.Fluch,);
          " $�` owt0ut.CoSu()�
      �0 $  (   i/put.�Ore(Y:
�           }  ) 0   }
 (( }}J