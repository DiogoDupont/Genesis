using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Genesis
{
    // ===================================================================
    //  Identidade do aplicativo
    // ===================================================================
    internal static class App
    {
        public const string Nome = "Genesis";
        public const string Versao = "1.0";
        public const string Slogan = "Onde a lógica nasce.";
        public const string Descricao = "Gerador de programas de CLP · TIA Portal Openness";

        /// <summary>Extensão dos projetos novos (os .fastproj antigos continuam abrindo).</summary>
        public const string Extensao = ".genesis";
        public const string FiltroArquivos = "Projeto Genesis (*.genesis;*.fastproj)|*.genesis;*.fastproj|Todos os arquivos|*.*";
    }

    // ===================================================================
    //  Tema (mesma família visual do Orquestra): cores, fontes e fábricas de controles
    // ===================================================================
    internal static class Tema
    {
        public static readonly Color Fundo = Color.FromArgb(7, 23, 40);
        public static readonly Color FundoEscuro = Color.FromArgb(6, 19, 33);
        public static readonly Color FundoLateral = Color.FromArgb(6, 19, 33);
        public static readonly Color Cartao = Color.FromArgb(14, 37, 62);
        public static readonly Color CartaoHover = Color.FromArgb(18, 54, 83);
        public static readonly Color Borda = Color.FromArgb(30, 70, 107);
        public static readonly Color BordaClara = Color.FromArgb(40, 97, 143);
        public static readonly Color Campo = Color.FromArgb(8, 27, 46);
        public static readonly Color CampoBorda = Color.FromArgb(40, 97, 143);
        public static readonly Color Texto = Color.FromArgb(242, 247, 252);
        public static readonly Color TextoSec = Color.FromArgb(169, 191, 211);
        public static readonly Color TextoFraco = Color.FromArgb(113, 140, 164);
        public static readonly Color Azul = Color.FromArgb(22, 131, 255);
        public static readonly Color AzulHover = Color.FromArgb(53, 164, 255);
        public static readonly Color AzulEscuro = Color.FromArgb(13, 99, 199);
        public static readonly Color Ciano = Color.FromArgb(64, 195, 255);
        public static readonly Color Verde = Color.FromArgb(32, 214, 138);
        public static readonly Color Amarelo = Color.FromArgb(245, 185, 74);
        public static readonly Color Vermelho = Color.FromArgb(255, 85, 112);
        public static readonly Color Selecao = Color.FromArgb(18, 58, 94);
        public static readonly Color CabecalhoGrade = Color.FromArgb(12, 36, 60);
        public static readonly Color LinhaGrade = Color.FromArgb(23, 54, 84);

        public static readonly bool NoWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

        private const string Familia = "Segoe UI";

        public static Font Fonte(float tamanho, FontStyle estilo = FontStyle.Regular)
        {
            return new Font(Familia, tamanho, estilo);
        }

        public static readonly Font Normal = Fonte(9.5f);
        public static readonly Font Negrito = Fonte(9.5f, FontStyle.Bold);
        public static readonly Font Pequena = Fonte(8.5f);
        public static readonly Font Rotulo = Fonte(7.75f, FontStyle.Bold);
        public static readonly Font Mono = new Font("Consolas", 9.5f);

        /// <summary>Caixa alta (rótulos de seção e de campo, como no Orquestra).</summary>
        public static string Espacado(string texto)
        {
            return (texto ?? "").ToUpper(new CultureInfo("pt-BR"));
        }

        /// <summary>Cor de fundo real atrás de um controle (sobe até achar uma cor opaca).</summary>
        public static Color FundoDe(Control c)
        {
            for (Control p = c != null ? c.Parent : null; p != null; p = p.Parent)
            {
                var cartao = p as Cartao;
                if (cartao != null) return cartao.CorFundo;
                if (p.BackColor.A == 255) return p.BackColor;
            }
            return Fundo;
        }

        // --- rótulos ----------------------------------------------------------
        public static Label Titulo(string texto, float tamanho = 20f)
        {
            return new Label
            {
                Text = texto,
                AutoSize = true,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = Fonte(tamanho, FontStyle.Bold),
                UseMnemonic = false
            };
        }

        public static Label Subtitulo(string texto)
        {
            return new Label
            {
                Text = texto,
                AutoSize = true,
                ForeColor = TextoSec,
                BackColor = Color.Transparent,
                Font = Fonte(9.75f),
                UseMnemonic = false
            };
        }

        /// <summary>Rótulo pequeno em caixa alta (ex.: "STATUS", "BUSCAR").</summary>
        public static Label Secao(string texto)
        {
            return new Label
            {
                Text = Espacado(texto),
                AutoSize = true,
                ForeColor = TextoSec,
                BackColor = Color.Transparent,
                Font = Rotulo,
                Margin = new Padding(0, 0, 0, 5),
                UseMnemonic = false
            };
        }

        /// <summary>Título de cartão (ex.: "Entradas e saídas").</summary>
        public static Label TituloCartao(string texto)
        {
            return new Label
            {
                Text = texto,
                AutoSize = true,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = Fonte(11.5f, FontStyle.Bold),
                UseMnemonic = false
            };
        }

        public static Label Ajuda(string texto, int largura = 0)
        {
            var l = new Label
            {
                Text = texto,
                ForeColor = TextoSec,
                BackColor = Color.Transparent,
                Font = Pequena,
                AutoSize = true,
                UseMnemonic = false
            };
            if (largura > 0) Medir(l, largura);
            return l;
        }

        /// <summary>Rótulo com quebra de linha e altura medida (o AutoSize com MaximumSize corta a última linha às vezes).</summary>
        public static void Medir(Label l, int largura)
        {
            l.AutoSize = false;
            Size t = TextRenderer.MeasureText(l.Text ?? "", l.Font, new Size(largura, 0), TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix);
            l.Size = new Size(largura, Math.Max(t.Height, l.Font.Height) + 2);
        }

        // --- botões -------------------------------------------------------------
        public static Button Botao(string texto, bool primario, EventHandler acao, string icone = null)
        {
            var b = new BotaoTema { Text = texto, Primario = primario, Icone = icone };
            if (acao != null) b.Click += acao;
            return b;
        }

        /// <summary>Botão quadrado só com ícone (barra do topo), com dica.</summary>
        public static Button BotaoIcone(string icone, string dica, EventHandler acao, ToolTip dicas)
        {
            var b = new BotaoTema { Icone = icone, Text = "", AutoSize = false, Size = new Size(36, 36), Margin = new Padding(0, 0, 6, 0), Discreto = true };
            if (acao != null) b.Click += acao;
            if (dicas != null) dicas.SetToolTip(b, dica);
            return b;
        }

        // --- campos -----------------------------------------------------------
        public static TextBox CaixaTexto()
        {
            return new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Campo,
                ForeColor = Texto,
                Font = Normal
            };
        }

        public static ComboBox Lista(bool editavel)
        {
            var c = new ListaTema
            {
                DropDownStyle = editavel ? ComboBoxStyle.DropDown : ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Campo,
                ForeColor = Texto,
                Font = Normal,
                IntegralHeight = false,
                MaxDropDownItems = 18
            };
            if (editavel)
            {
                c.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                c.AutoCompleteSource = AutoCompleteSource.ListItems;
            }
            return c;
        }

        /// <summary>
        /// Moldura arredondada em volta de uma caixa de texto ou lista (o campo "alto" do Orquestra).
        /// Outros controles (caixinha de marcar, painel) voltam como vieram.
        /// </summary>
        public static Control Emoldurar(Control c)
        {
            if (c is TextBox || c is ComboBox) return new Moldura(c);
            return c;
        }

        public static CheckBox Marcador(string texto)
        {
            return new CheckBox
            {
                Text = texto,
                AutoSize = true,
                ForeColor = Texto,
                BackColor = Color.Transparent,
                Font = Normal,
                FlatStyle = FlatStyle.Standard,
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
        }

        // --- grade --------------------------------------------------------------
        public static void Grade(DataGridView g)
        {
            g.BackgroundColor = Cartao;
            g.BorderStyle = BorderStyle.None;
            g.GridColor = LinhaGrade;
            g.EnableHeadersVisualStyles = false;
            g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            g.ColumnHeadersHeight = 36;
            g.RowTemplate.Height = 32;
            g.Font = Normal;

            g.ColumnHeadersDefaultCellStyle.BackColor = CabecalhoGrade;
            g.ColumnHeadersDefaultCellStyle.ForeColor = TextoSec;
            g.ColumnHeadersDefaultCellStyle.SelectionBackColor = CabecalhoGrade;
            g.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextoSec;
            g.ColumnHeadersDefaultCellStyle.Font = Fonte(7.75f, FontStyle.Bold);
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 0, 0);

            g.RowHeadersDefaultCellStyle.BackColor = Cartao;
            g.RowHeadersDefaultCellStyle.SelectionBackColor = Selecao;

            g.DefaultCellStyle.BackColor = Cartao;
            g.DefaultCellStyle.ForeColor = Texto;
            g.DefaultCellStyle.SelectionBackColor = Selecao;
            g.DefaultCellStyle.SelectionForeColor = Color.White;
            g.DefaultCellStyle.Padding = new Padding(6, 0, 0, 0);
            g.AlternatingRowsDefaultCellStyle.BackColor = Cartao;
            g.HandleCreated += delegate { Win.BarrasEscuras(g); };
        }

        /// <summary>Aplica o tema em controles comuns (janelas simples, ex.: adicionar em lote).</summary>
        public static void Aplicar(Control raiz)
        {
            raiz.BackColor = raiz is Form ? Fundo : raiz.BackColor;
            raiz.ForeColor = Texto;
            foreach (Control c in raiz.Controls)
            {
                if (c is TextBox) { c.BackColor = Campo; c.ForeColor = Texto; ((TextBox)c).BorderStyle = BorderStyle.FixedSingle; }
                else if (c is NumericUpDown) { c.BackColor = Campo; c.ForeColor = Texto; ((NumericUpDown)c).BorderStyle = BorderStyle.FixedSingle; }
                else if (c is ComboBox) { c.BackColor = Campo; c.ForeColor = Texto; ((ComboBox)c).FlatStyle = FlatStyle.Flat; }
                else if (c is Label || c is CheckBox) { c.ForeColor = Texto; }
                else if (c is Panel && !(c is Cartao) && !(c is Moldura)) { c.BackColor = Color.Transparent; }
                Aplicar(c);
            }
        }

        public static GraphicsPath Arredondado(Rectangle r, int raio)
        {
            var p = new GraphicsPath();
            int d = raio * 2;
            if (d <= 0 || r.Width < d || r.Height < d) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        internal static void PintarCartao(Graphics g, Rectangle r, Color fundoPai, Color fundo, Color borda, int raio)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bPai = new SolidBrush(fundoPai)) g.FillRectangle(bPai, r);
            var rr = new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1);
            using (GraphicsPath p = Arredondado(rr, raio))
            using (var b = new SolidBrush(fundo))
            using (var pen = new Pen(borda))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }
        }

        /// <summary>Faixa azul à esquerda dos cartões de seção (detalhe do Orquestra).</summary>
        internal static void PintarBarra(Graphics g, Rectangle r, int raio)
        {
            var rr = new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1);
            using (GraphicsPath p = Arredondado(rr, raio))
            {
                Region antes = g.Clip;
                g.SetClip(p, CombineMode.Intersect);
                var faixa = new Rectangle(r.X, r.Y, 3, r.Height);
                using (var b = new LinearGradientBrush(new Rectangle(r.X, r.Y, 3, Math.Max(1, r.Height)), AzulHover, AzulEscuro, LinearGradientMode.Vertical))
                    g.FillRectangle(b, faixa);
                g.Clip = antes;
            }
        }

        // --- recursos embutidos (logo, ícone) -----------------------------------------
        private static readonly Dictionary<string, Image> _imagens = new Dictionary<string, Image>();

        private static Stream Recurso(string nome)
        {
            Assembly a = typeof(Tema).Assembly;
            string achado = a.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(nome, StringComparison.OrdinalIgnoreCase));
            return achado != null ? a.GetManifestResourceStream(achado) : null;
        }

        /// <summary>Imagem embutida (logo.png, marca.png); null se o recurso não existir.</summary>
        public static Image Imagem(string nome)
        {
            Image im;
            if (_imagens.TryGetValue(nome, out im)) return im;
            try
            {
                using (Stream s = Recurso(nome))
                    im = s != null ? new Bitmap(Image.FromStream(s)) : null;
            }
            catch { im = null; }
            _imagens[nome] = im;
            return im;
        }

        public static Icon IconeApp()
        {
            try
            {
                using (Stream s = Recurso("genesis.ico"))
                    if (s != null) return new Icon(s);
            }
            catch { /* segue com o ícone padrão */ }
            return null;
        }

        /// <summary>Janela com fundo, fonte, ícone e barra de título do tema.</summary>
        public static void Janela(Form f)
        {
            f.BackColor = Fundo;
            f.ForeColor = Texto;
            f.Font = Normal;
            Icon ic = IconeApp();
            if (ic != null) f.Icon = ic;
            f.HandleCreated += delegate { Win.TituloEscuro(f); };
        }
    }

    // ===================================================================
    //  Ajustes do Windows: barra de título e barras de rolagem escuras
    // ===================================================================
    internal static class Win
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int valor, int tamanho);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string app, string ids);

        public static void TituloEscuro(Form f)
        {
            if (!Tema.NoWindows) return;
            try
            {
                int sim = 1;
                DwmSetWindowAttribute(f.Handle, 19, ref sim, 4);   // Windows 10 antigo
                DwmSetWindowAttribute(f.Handle, 20, ref sim, 4);   // modo escuro
                int cor = Tema.FundoEscuro.R | (Tema.FundoEscuro.G << 8) | (Tema.FundoEscuro.B << 16);
                DwmSetWindowAttribute(f.Handle, 35, ref cor, 4);   // Windows 11: cor da barra de título
            }
            catch { /* Windows sem esse recurso: fica a barra padrão */ }
        }

        public static void BarrasEscuras(Control c)
        {
            if (!Tema.NoWindows || c == null) return;
            try
            {
                SetWindowTheme(c.Handle, "DarkMode_Explorer", null);
                foreach (Control f in c.Controls)
                    if (f is ScrollBar) SetWindowTheme(f.Handle, "DarkMode_Explorer", null);
            }
            catch { /* sem tema escuro: barras padrão */ }
        }

        /// <summary>Barras de rolagem escuras assim que o controle for criado.</summary>
        public static void Escurecer(Control c)
        {
            if (c.IsHandleCreated) BarrasEscuras(c);
            c.HandleCreated += delegate { BarrasEscuras(c); };
        }
    }

    // ===================================================================
    //  Ícones de linha, desenhados em vetor (grade 24 x 24)
    //    M x y x y ...   linha        P x y ...   polígono fechado
    //    C cx cy r       círculo      D cx cy r   ponto cheio
    //    R x y w h r     retângulo    B x1 y1 ... x4 y4   curva
    // ===================================================================
    internal static class Icones
    {
        private static readonly Dictionary<string, string> Desenhos = new Dictionary<string, string>
        {
            { "inicio", "M 3 11 12 3 21 11; M 5 9.5 5 20 19 20 19 9.5; M 10 20 10 14 14 14 14 20" },
            { "tags", "P 3 3 11.5 3 21 12.5 12.5 21 3 11.5; C 7.6 7.6 1.4" },
            { "maquinas", "R 9 3 6 5 1.2; R 3 16 6 5 1.2; R 15 16 6 5 1.2; M 12 8 12 12; M 6 16 6 12 18 12 18 16" },
            { "partidas", "P 13 2 4 14 11 14 10 22 20 9.5 13 9.5" },
            { "sensores", "C 12 12 9.2; M 12 12 16 7.5; M 12 5.2 12 6.6; M 5.2 12 6.6 12; M 18.8 12 17.4 12; D 12 12 1.1" },
            { "pids", "M 2 12 6 12 9 4 15 20 18 12 22 12" },
            { "modbus", "R 3 4 18 16 2; M 3 9.5 21 9.5; M 3 14.7 21 14.7; M 9.5 4 9.5 20" },
            { "config", "M 3 6 7 6; M 11 6 21 6; C 9 6 2; M 3 12 13 12; M 17 12 21 12; C 15 12 2; M 3 18 5 18; M 9 18 21 18; C 7 18 2" },
            { "gerar", "C 12 12 9.5; P 10 8 16 12 10 16" },
            { "voltar", "M 19 12 5 12; M 11 6 5 12 11 18" },
            { "abrir", "P 3 6 10 6 12 9 21 9 21 19 3 19" },
            { "salvar", "P 4 4 16 4 20 8 20 20 4 20; M 8 4 8 9 14 9 14 4; M 7 20 7 14 17 14 17 20" },
            { "novo", "P 6 3 14 3 19 8 19 21 6 21; M 14 3 14 8 19 8; M 12.5 11.5 12.5 17.5; M 9.5 14.5 15.5 14.5" },
            { "mais", "M 12 5 12 19; M 5 12 19 12" },
            { "menu", "D 5 12 1.5; D 12 12 1.5; D 19 12 1.5" },
            { "check", "M 5 12.5 10 17.5 19 7" },
            { "ok", "C 12 12 9.5; M 7.8 12.3 10.8 15.3 16.4 9.2" },
            { "alerta", "P 12 3.5 22 20 2 20; M 12 10 12 14.5; D 12 17.2 1" },
            { "info", "C 12 12 9.5; M 12 11 12 16.5; D 12 7.8 1" },
            { "erro", "C 12 12 9.5; M 9 9 15 15; M 15 9 9 15" },
            { "pergunta", "C 12 12 9.5; B 9.3 9.4 9.6 6.2 14.8 6.2 14.8 9.6; B 14.8 9.6 14.8 11.6 12 11.6 12 13.8; D 12 17 1" },
            { "lixeira", "M 4 7 20 7; M 9 7 9 4 15 4 15 7; M 6 7 7 20 17 20 18 7; M 10 11 10 16; M 14 11 14 16" },
            { "duplicar", "R 8 8 12 12 2; M 5 16 4 16 4 4 16 4 16 5" },
            { "colar", "R 5 5 14 16 2; R 9 3 6 4 1; M 9 12 15 12; M 9 16 13 16" },
            { "buscar", "C 11 11 6.5; M 16 16 20.5 20.5" },
            { "flotador", "B 2 7 5 4 8 10 12 7; B 12 7 16 4 19 10 22 7; B 2 12.5 5 9.5 8 15.5 12 12.5; B 12 12.5 16 9.5 19 15.5 22 12.5; B 2 18 5 15 8 21 12 18; B 12 18 16 15 19 21 22 18" },
            { "preparador", "M 9 3 15 3; M 10 3 10 9 4.5 19 5.5 21 18.5 21 19.5 19 14 9 14 3; M 7.2 15 16.8 15" },
            { "lote", "M 3 6 15 6; M 3 12 11 12; M 3 18 11 18; M 18 12 18 20; M 14 16 22 16" },
            { "seta", "M 9 6 15 12 9 18" },
            { "baixo", "M 6 9 12 15 18 9" },
            { "cima", "M 6 15 12 9 18 15" },
            { "exportar", "M 12 4 12 15; M 7 10.5 12 15.5 17 10.5; M 4 20 20 20" },
            { "organizar", "M 4 6 20 6; M 4 12 15 12; M 4 18 10 18" },
            { "log", "R 3 4 18 16 2; M 7 9.5 10 12 7 14.5; M 12.5 15 16.5 15" },
            { "tia", "R 6 6 12 12 2; R 9.5 9.5 5 5 0.8; M 9 3 9 6; M 15 3 15 6; M 9 18 9 21; M 15 18 15 21; M 3 9 6 9; M 3 15 6 15; M 18 9 21 9; M 18 15 21 15" },
            { "lista", "M 10 6 21 6; M 10 12 21 12; M 10 18 21 18; M 3 6 4.5 7.5 7 4.5; M 3 12 4.5 13.5 7 10.5; M 3 18 4.5 19.5 7 16.5" },
            { "soltar", "C 12 12 9.5; M 8 12 16 12" },
            { "fechar", "M 6 6 18 18; M 18 6 6 18" },
            { "blocos", "P 12 3 21 7.5 12 12 3 7.5; M 3 12 12 16.5 21 12; M 3 16.5 12 21 21 16.5" },
            { "faisca", "B 12 2 12.8 8.5 15.5 11.2 22 12; B 22 12 15.5 12.8 12.8 15.5 12 22; B 12 22 11.2 15.5 8.5 12.8 2 12; B 2 12 8.5 11.2 11.2 8.5 12 2" }
        };

        public static bool Existe(string nome) { return nome != null && Desenhos.ContainsKey(nome); }

        public static void Desenhar(Graphics g, string nome, RectangleF r, Color cor, float espessura = 0)
        {
            string d;
            if (nome == null || !Desenhos.TryGetValue(nome, out d)) return;
            float e = Math.Min(r.Width, r.Height) / 24f;
            float esp = espessura > 0 ? espessura : Math.Max(1.4f, 1.9f * e);
            SmoothingMode antes = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(cor, esp) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            using (var brush = new SolidBrush(cor))
            {
                foreach (string parte in d.Split(';'))
                {
                    string[] t = parte.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (t.Length < 3) continue;
                    float[] n = t.Skip(1).Select(x => float.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                    Func<int, PointF> pt = i => new PointF(r.X + n[i] * e, r.Y + n[i + 1] * e);
                    switch (t[0])
                    {
                        case "M":
                        case "P":
                        {
                            var pts = new PointF[n.Length / 2];
                            for (int i = 0; i < pts.Length; i++) pts[i] = pt(i * 2);
                            if (t[0] == "P") g.DrawPolygon(pen, pts); else g.DrawLines(pen, pts);
                            break;
                        }
                        case "C":
                            g.DrawEllipse(pen, r.X + (n[0] - n[2]) * e, r.Y + (n[1] - n[2]) * e, 2 * n[2] * e, 2 * n[2] * e);
                            break;
                        case "D":
                            g.FillEllipse(brush, r.X + (n[0] - n[2]) * e, r.Y + (n[1] - n[2]) * e, 2 * n[2] * e, 2 * n[2] * e);
                            break;
                        case "R":
                        {
                            var rr = new RectangleF(r.X + n[0] * e, r.Y + n[1] * e, n[2] * e, n[3] * e);
                            float raio = n[4] * e;
                            using (var p = new GraphicsPath())
                            {
                                float dd = raio * 2;
                                if (dd < 0.5f) p.AddRectangle(rr);
                                else
                                {
                                    p.AddArc(rr.X, rr.Y, dd, dd, 180, 90);
                                    p.AddArc(rr.Right - dd, rr.Y, dd, dd, 270, 90);
                                    p.AddArc(rr.Right - dd, rr.Bottom - dd, dd, dd, 0, 90);
                                    p.AddArc(rr.X, rr.Bottom - dd, dd, dd, 90, 90);
                                    p.CloseFigure();
                                }
                                g.DrawPath(pen, p);
                            }
                            break;
                        }
                        case "B":
                            g.DrawBezier(pen, pt(0), pt(2), pt(4), pt(6));
                            break;
                    }
                }
            }
            g.SmoothingMode = antes;
        }
    }

    // ===================================================================
    //  Cartão: painel com cantos arredondados e borda sutil
    // ===================================================================
    internal class Cartao : Panel
    {
        public int Raio = 12;
        public Color CorBorda = Tema.Borda;
        public Color CorFundo = Tema.Cartao;
        public bool BarraAzul;   // faixa azul à esquerda (como os blocos do Orquestra)

        public Cartao()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Tema.Cartao;
            Padding = new Padding(20);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Tema.PintarCartao(e.Graphics, ClientRectangle, Tema.FundoDe(this), CorFundo, CorBorda, Raio);
            if (BarraAzul) Tema.PintarBarra(e.Graphics, ClientRectangle, Raio);
        }
    }

    /// <summary>Cartão em TableLayoutPanel: cresce com o conteúdo (editor, configuração, janelas).</summary>
    internal class CartaoTabela : TableLayoutPanel
    {
        public int Raio = 12;
        public bool BarraAzul = true;

        public CartaoTabela() : this(2) { }

        public CartaoTabela(int colunas)
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Tema.Cartao;
            Padding = new Padding(22, 16, 20, 16);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ColumnCount = colunas;
            if (colunas == 2)
            {
                ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 215));
                ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            }
            else
            {
                for (int i = 0; i < colunas; i++) ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / colunas));
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Tema.PintarCartao(e.Graphics, ClientRectangle, Tema.FundoDe(this), Tema.Cartao, Tema.Borda, Raio);
            if (BarraAzul) Tema.PintarBarra(e.Graphics, ClientRectangle, Raio);
        }
    }

    // ===================================================================
    //  Faixa escura com linha embaixo (barra do topo e cabeçalho das janelas)
    // ===================================================================
    internal class FundoDegrade : Panel
    {
        public FundoDegrade()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Tema.FundoEscuro;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (Width <= 0 || Height <= 0) return;
            using (var b = new SolidBrush(Tema.FundoEscuro))
                e.Graphics.FillRectangle(b, ClientRectangle);
            using (var p = new Pen(Tema.Borda))
                e.Graphics.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }
    }

    // ===================================================================
    //  Moldura arredondada de um campo (caixa de texto ou lista)
    // ===================================================================
    internal class Moldura : Panel
    {
        public readonly Control Interno;
        private bool _foco, _sobre;
        public const int AlturaPadrao = 36;

        public Moldura(Control interno)
        {
            Interno = interno;
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Tema.Campo;
            Cursor = Cursors.IBeam;
            Margin = interno.Margin;

            var tb = interno as TextBox;
            if (tb != null) tb.BorderStyle = BorderStyle.None;
            interno.BackColor = Tema.Campo;

            bool varias = tb != null && tb.Multiline;
            int altInterno = varias ? interno.Height : interno.PreferredSize.Height;
            if (tb != null && !varias) altInterno = tb.PreferredHeight;
            int padV = varias ? 8 : Math.Max(3, (AlturaPadrao - altInterno) / 2);
            int padH = interno is ComboBox ? 4 : 11;
            Padding = new Padding(padH, padV, interno is ComboBox ? 3 : 8, padV);
            Height = altInterno + 2 * padV;
            Width = Math.Max(60, interno.Width + Padding.Horizontal);
            if (interno is ComboBox) Cursor = Cursors.Default;

            interno.Dock = DockStyle.Fill;
            interno.Margin = new Padding(0);
            Controls.Add(interno);
            interno.Enter += delegate { _foco = true; Invalidate(); };
            interno.Leave += delegate { _foco = false; Invalidate(); };
            interno.MouseEnter += delegate { _sobre = true; Invalidate(); };
            interno.MouseLeave += delegate { _sobre = ClientRectangle.Contains(PointToClient(MousePosition)); Invalidate(); };
            Click += delegate { interno.Focus(); };
        }

        protected override void OnMouseEnter(EventArgs e) { _sobre = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _sobre = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            Color borda = _foco ? Tema.Azul : _sobre ? Color.FromArgb(52, 121, 170) : Tema.BordaClara;
            Tema.PintarCartao(e.Graphics, ClientRectangle, Tema.FundoDe(this), Tema.Campo, borda, 7);
        }
    }

    /// <summary>Lista (ComboBox) plana: esconde a borda nativa e desenha a seta no tom do tema.</summary>
    internal class ListaTema : ComboBox
    {
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x000F && Tema.NoWindows) PintarSeta();   // WM_PAINT
        }

        private void PintarSeta()
        {
            try
            {
                using (Graphics g = Graphics.FromHwnd(Handle))
                {
                    int lb = SystemInformation.VerticalScrollBarWidth;
                    var botao = new Rectangle(Width - lb - 1, 0, lb + 1, Height);
                    using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, botao);
                    using (var p = new Pen(BackColor)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
                    float cx = botao.X + botao.Width / 2f, cy = Height / 2f;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var p = new Pen(Enabled ? Tema.Ciano : Tema.TextoFraco, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                        g.DrawLines(p, new[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2.5f), new PointF(cx + 4, cy - 2) });
                }
            }
            catch { /* só visual */ }
        }
    }

    // ===================================================================
    //  Botão arredondado (primário em degradê azul, secundário com contorno)
    // ===================================================================
    internal class BotaoTema : Button
    {
        public bool Primario;
        public bool Discreto;      // sem contorno até passar o mouse (ícones da barra do topo)
        public bool Perigo;        // contorno vermelho ao passar o mouse (excluir)
        public string Icone;
        private bool _sobre, _apertado;

        public BotaoTema()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            MinimumSize = new Size(0, 38);
            Margin = new Padding(0, 0, 10, 0);
            Cursor = Cursors.Hand;
            Font = Tema.Fonte(9f, FontStyle.Bold);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            UseMnemonic = false;
            BackColor = Tema.Cartao;
            ForeColor = Tema.Texto;
            TabStop = true;
        }

        private string TextoMostrado { get { return Primario ? Tema.Espacado(Text) : (Text ?? ""); } }

        public override Size GetPreferredSize(Size proposto)
        {
            Size t = TextRenderer.MeasureText(TextoMostrado.Length > 0 ? TextoMostrado : "X", Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine);
            int w = (TextoMostrado.Length > 0 ? t.Width : 0) + (Primario ? 38 : 30) + (Icones.Existe(Icone) ? (TextoMostrado.Length > 0 ? 24 : 16) : 0);
            return new Size(Math.Max(w, MinimumSize.Width), Math.Max(t.Height + 16, MinimumSize.Height));
        }

        protected override void OnMouseEnter(EventArgs e) { _sobre = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _sobre = false; _apertado = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _apertado = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _apertado = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bPai = new SolidBrush(Tema.FundoDe(this))) g.FillRectangle(bPai, ClientRectangle);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color texto;

            using (GraphicsPath p = Tema.Arredondado(r, 7))
            {
                if (!Enabled)
                {
                    using (var b = new SolidBrush(Color.FromArgb(16, 40, 64))) g.FillPath(b, p);
                    using (var pen = new Pen(Tema.Borda)) g.DrawPath(pen, p);
                    texto = Tema.TextoFraco;
                }
                else if (Primario)
                {
                    Color c1 = _apertado ? Tema.AzulEscuro : _sobre ? Color.FromArgb(62, 160, 255) : Color.FromArgb(34, 142, 255);
                    Color c2 = _apertado ? Color.FromArgb(10, 84, 172) : _sobre ? Color.FromArgb(22, 118, 232) : Tema.AzulEscuro;
                    using (var b = new LinearGradientBrush(new Rectangle(0, 0, Width, Math.Max(1, Height)), c1, c2, LinearGradientMode.Vertical)) g.FillPath(b, p);
                    using (var pen = new Pen(Color.FromArgb(70, 170, 255))) g.DrawPath(pen, p);
                    texto = Color.White;
                }
                else
                {
                    Color fundo = _apertado ? Tema.Selecao : _sobre ? Tema.CartaoHover : Discreto ? Color.Transparent : Color.FromArgb(11, 33, 55);
                    Color borda = _sobre ? (Perigo ? Tema.Vermelho : Tema.Azul) : Discreto ? Color.Transparent : Tema.BordaClara;
                    if (fundo.A > 0) using (var b = new SolidBrush(fundo)) g.FillPath(b, p);
                    if (borda.A > 0) using (var pen = new Pen(borda)) g.DrawPath(pen, p);
                    texto = _sobre && Perigo ? Tema.Vermelho : Color.FromArgb(220, 238, 255);
                }
            }
            if (Focused && ShowFocusCues && Enabled)
                using (GraphicsPath pf = Tema.Arredondado(new Rectangle(2, 2, Width - 5, Height - 5), 5))
                using (var pen = new Pen(Color.FromArgb(120, 255, 255, 255)) { DashStyle = DashStyle.Dot })
                    g.DrawPath(pen, pf);

            string t = TextoMostrado;
            bool temIcone = Icones.Existe(Icone);
            int ti = 16;
            Size tt = t.Length > 0 ? TextRenderer.MeasureText(t, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine) : Size.Empty;
            int total = tt.Width + (temIcone ? ti + (t.Length > 0 ? 6 : 0) : 0);
            int x = (Width - total) / 2;
            if (temIcone)
            {
                Color ci = !Enabled ? Tema.TextoFraco : Primario ? Color.White : (_sobre && Perigo ? Tema.Vermelho : Tema.Ciano);
                Icones.Desenhar(g, Icone, new RectangleF(x, (Height - ti) / 2f, ti, ti), ci, 1.6f);
                x += ti + 6;
            }
            if (t.Length > 0)
                TextRenderer.DrawText(g, t, Font, new Rectangle(x, 0, tt.Width + 4, Height), texto, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }
    }

    // ===================================================================
    //  Item da navegação do topo: nome, contador e aviso de pendência
    // ===================================================================
    internal class ItemNav : Control
    {
        public string Icone;
        public string Contador = "";
        public bool Pendente;        // contador em amarelo: há itens com campos a preencher
        public bool Destaque;        // "Gerar": texto em ciano
        private bool _selecionado, _sobre;

        public bool Selecionado
        {
            get { return _selecionado; }
            set { if (_selecionado != value) { _selecionado = value; Invalidate(); } }
        }

        public ItemNav(string texto, string icone)
        {
            Text = texto;
            Icone = icone;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            BackColor = Color.Transparent;
            Font = Tema.Fonte(9.5f, FontStyle.Bold);
            Margin = new Padding(0);
            Ajustar();
        }

        public void Definir(string contador, bool pendente)
        {
            if (Contador == contador && Pendente == pendente) return;
            Contador = contador;
            Pendente = pendente;
            Ajustar();
            Invalidate();
        }

        private int LarguraTexto() { return TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width; }
        private int LarguraContador()
        {
            if (string.IsNullOrEmpty(Contador)) return 0;
            using (Font f = Tema.Fonte(8f, FontStyle.Bold))
                return TextRenderer.MeasureText(Contador, f, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width + 12;
        }

        private void Ajustar()
        {
            int lc = LarguraContador();
            Width = 14 + (Text.Length > 0 ? LarguraTexto() : 18) + (lc > 0 ? 6 + lc : 0) + 14;
        }

        protected override void OnMouseEnter(EventArgs e) { _sobre = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _sobre = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (_sobre || _selecionado)
            {
                var r = new Rectangle(3, 10, Width - 7, Height - 21);
                using (GraphicsPath p = Tema.Arredondado(r, 7))
                using (var b = new SolidBrush(_selecionado ? Color.FromArgb(60, 22, 131, 255) : Color.FromArgb(28, 255, 255, 255)))
                    g.FillPath(b, p);
            }
            Color cor = _selecionado ? Color.White : Destaque ? Tema.Ciano : _sobre ? Tema.Texto : Tema.TextoSec;
            int x = 14;
            if (Text.Length == 0)
            {
                Icones.Desenhar(g, Icone, new RectangleF((Width - 18) / 2f, (Height - 18) / 2f - 1, 18, 18), _selecionado ? Tema.Ciano : cor, 1.7f);
            }
            else
            {
                int lt = LarguraTexto();
                TextRenderer.DrawText(g, Text, Font, new Rectangle(x, 0, lt + 4, Height - 2), cor, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
                x += lt + 6;
            }
            int lc = LarguraContador();
            if (lc > 0)
            {
                var rc = new Rectangle(x, (Height - 18) / 2 - 1, lc, 18);
                using (GraphicsPath p = Tema.Arredondado(rc, 8))
                using (var b = new SolidBrush(Pendente ? Color.FromArgb(70, 52, 14) : Color.FromArgb(20, 60, 98)))
                    g.FillPath(b, p);
                using (Font f = Tema.Fonte(8f, FontStyle.Bold))
                    TextRenderer.DrawText(g, Contador, f, rc, Pendente ? Tema.Amarelo : Color.FromArgb(191, 233, 255), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
            }
            if (_selecionado)
            {
                var rl = new Rectangle(12, Height - 4, Width - 24, 3);
                using (var b = new LinearGradientBrush(new Rectangle(rl.X, rl.Y, Math.Max(1, rl.Width), 3), Tema.Ciano, Tema.Azul, LinearGradientMode.Horizontal))
                using (GraphicsPath p = Tema.Arredondado(rl, 1))
                    g.FillPath(b, p);
            }
        }
    }

    // ===================================================================
    //  Cartão clicável (tela inicial e catálogo): ícone, título, descrição e situação
    // ===================================================================
    internal class CartaoInicio : Control
    {
        public string Icone, Descricao;
        public string Etapa = "";            // "ETAPA 1"
        public string Situacao = "";         // linha de baixo (ex.: "42 tags · 3 sem endereço")
        public Color CorSituacao = Tema.Ciano;
        public bool Destaque;                // cartão principal (Gerar)
        public bool Clicavel = true;
        private bool _sobre;

        public CartaoInicio(string titulo, string descricao, string icone)
        {
            Text = titulo;
            Descricao = descricao;
            Icone = icone;
            Size = new Size(330, 176);
            Margin = new Padding(0, 0, 16, 16);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            BackColor = Tema.Fundo;
        }

        public void Definir(string situacao, Color cor)
        {
            if (Situacao == situacao && CorSituacao == cor) return;
            Situacao = situacao;
            CorSituacao = cor;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { _sobre = Clicavel; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _sobre = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bPai = new SolidBrush(Tema.FundoDe(this))) g.FillRectangle(bPai, ClientRectangle);

            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color c1, c2, borda;
            if (Destaque)
            {
                c1 = _sobre ? Color.FromArgb(26, 92, 168) : Color.FromArgb(20, 78, 146);
                c2 = _sobre ? Color.FromArgb(12, 48, 92) : Color.FromArgb(10, 40, 78);
                borda = _sobre ? Tema.Ciano : Tema.AzulHover;
            }
            else
            {
                c1 = _sobre ? Color.FromArgb(22, 57, 92) : Color.FromArgb(18, 48, 77);
                c2 = _sobre ? Color.FromArgb(11, 34, 57) : Color.FromArgb(9, 29, 49);
                borda = _sobre ? Tema.Azul : Color.FromArgb(32, 84, 125);
            }
            using (GraphicsPath p = Tema.Arredondado(r, 14))
            using (var b = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height)), c1, c2, 55f))
            using (var pen = new Pen(borda))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }

            int m = 22;
            Icones.Desenhar(g, Icone, new RectangleF(m, 20, 38, 38), Destaque ? Color.White : Tema.Ciano, 2.4f);

            if (!string.IsNullOrEmpty(Etapa))
                using (Font f = Tema.Fonte(7.5f, FontStyle.Bold))
                    TextRenderer.DrawText(g, Etapa, f, new Rectangle(Width - 130 - m, 22, 130, 16), Destaque ? Color.FromArgb(200, 232, 255) : Tema.TextoFraco, TextFormatFlags.Right | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);

            bool temSituacao = !string.IsNullOrEmpty(Situacao);
            int yT = 66;
            using (Font fT = Tema.Fonte(12.5f, FontStyle.Bold))
            {
                TextRenderer.DrawText(g, Text, fT, new Rectangle(m - 1, yT, Width - 2 * m, 26), Color.White, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
            int yD = yT + 27;
            int hD = Height - yD - (temSituacao ? 44 : 12);
            using (Font fD = Tema.Fonte(9f))
                TextRenderer.DrawText(g, Descricao ?? "", fD, new Rectangle(m, yD, Width - 2 * m, hD), Destaque ? Color.FromArgb(205, 226, 245) : Tema.TextoSec,
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl);

            if (temSituacao)
            {
                using (var pen = new Pen(Color.FromArgb(Destaque ? 70 : 46, 255, 255, 255)))
                    g.DrawLine(pen, m, Height - 40, Width - m, Height - 40);
                using (Font fS = Tema.Fonte(8.75f, FontStyle.Bold))
                    TextRenderer.DrawText(g, Situacao, fS, new Rectangle(m, Height - 33, Width - 2 * m, 20), CorSituacao, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            }
        }
    }

    // ===================================================================
    //  Opção de escolha única (página Gerar): título, explicação e marcador
    // ===================================================================
    internal class OpcaoCartao : Control
    {
        public string Descricao = "";
        public string Aviso = "";            // linha em amarelo (o que se perde, o que falta)
        private bool _marcada, _sobre;
        public event EventHandler Escolhida;

        public bool Marcada
        {
            get { return _marcada; }
            set { if (_marcada != value) { _marcada = value; Invalidate(); } }
        }

        public OpcaoCartao(string titulo, string descricao)
        {
            Text = titulo;
            Descricao = descricao;
            Height = 78;
            Cursor = Cursors.Hand;
            Margin = new Padding(0, 0, 0, 8);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        }

        /// <summary>Altura para o texto caber na largura atual.</summary>
        public void Ajustar()
        {
            int lt = Math.Max(120, Width - 46 - 16);
            using (Font f = Tema.Fonte(8.75f))
            {
                int h = 12 + 22;
                h += TextRenderer.MeasureText(Descricao ?? "", f, new Size(lt, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl).Height + 2;
                if (!string.IsNullOrEmpty(Aviso))
                    h += TextRenderer.MeasureText(Aviso, f, new Size(lt, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl).Height + 4;
                Height = h + 12;
            }
        }

        protected override void OnMouseEnter(EventArgs e) { _sobre = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _sobre = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (Escolhida != null) Escolhida(this, EventArgs.Empty);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bPai = new SolidBrush(Tema.FundoDe(this))) g.FillRectangle(bPai, ClientRectangle);
            var r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Tema.Arredondado(r, 9))
            using (var b = new SolidBrush(_marcada ? Color.FromArgb(16, 52, 92) : _sobre ? Color.FromArgb(13, 40, 66) : Tema.Campo))
            using (var pen = new Pen(_marcada ? Tema.Azul : _sobre ? Tema.BordaClara : Tema.Borda, _marcada ? 1.6f : 1f))
            {
                g.FillPath(b, p);
                g.DrawPath(pen, p);
            }
            // marcador redondo
            var rm = new Rectangle(15, 14, 17, 17);
            using (var pen = new Pen(_marcada ? Tema.Ciano : Tema.BordaClara, 1.6f)) g.DrawEllipse(pen, rm);
            if (_marcada) using (var b = new SolidBrush(Tema.Ciano)) g.FillEllipse(b, rm.X + 4.5f, rm.Y + 4.5f, 8, 8);

            int x = 46, lt = Math.Max(120, Width - x - 16), y = 11;
            using (Font fT = Tema.Fonte(10.25f, FontStyle.Bold))
                TextRenderer.DrawText(g, Text, fT, new Rectangle(x - 1, y, lt, 22), Color.White, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
            y += 23;
            using (Font f = Tema.Fonte(8.75f))
            {
                int hd = TextRenderer.MeasureText(Descricao ?? "", f, new Size(lt, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl).Height;
                TextRenderer.DrawText(g, Descricao ?? "", f, new Rectangle(x, y, lt, hd + 2), Tema.TextoSec, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl);
                y += hd + 4;
                if (!string.IsNullOrEmpty(Aviso))
                    TextRenderer.DrawText(g, Aviso, f, new Rectangle(x, y, lt, Height - y), Tema.Amarelo, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl);
            }
        }
    }

    // ===================================================================
    //  Linha da conferência: ícone de situação, texto e seta (quando leva a algum lugar)
    // ===================================================================
    internal class LinhaConferencia : Control
    {
        public string Icone = "ok";
        public Color Cor = Tema.Verde;
        public string Detalhe = "";
        public bool Recuo;                // item de uma lista (recuado, fonte menor)
        public Action Acao;
        private bool _sobre;

        public LinhaConferencia(string icone, Color cor, string texto, string detalhe, Action acao, bool recuo = false)
        {
            Icone = icone; Cor = cor; Text = texto; Detalhe = detalhe ?? ""; Acao = acao; Recuo = recuo;
            Dock = DockStyle.Top;
            Height = recuo ? 28 : (Detalhe.Length > 0 ? 50 : 36);
            Cursor = acao != null ? Cursors.Hand : Cursors.Default;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _sobre = Acao != null; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _sobre = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { base.OnClick(e); if (Acao != null) Acao(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bPai = new SolidBrush(Tema.FundoDe(this))) g.FillRectangle(bPai, ClientRectangle);
            if (_sobre)
                using (GraphicsPath p = Tema.Arredondado(new Rectangle(0, 1, Width - 1, Height - 3), 6))
                using (var b = new SolidBrush(Tema.CartaoHover))
                    g.FillPath(b, p);

            int x = Recuo ? 34 : 8;
            if (!Recuo) Icones.Desenhar(g, Icone, new RectangleF(x, Detalhe.Length > 0 ? 8 : (Height - 18) / 2f, 18, 18), Cor, 1.8f);
            else using (var b = new SolidBrush(Cor)) g.FillEllipse(b, x, Height / 2f - 2.5f, 5, 5);
            x += Recuo ? 14 : 28;
            int lt = Width - x - (Acao != null ? 26 : 8);
            using (Font f = Recuo ? Tema.Fonte(8.75f) : Tema.Fonte(9.5f, FontStyle.Bold))
            {
                var rt = Detalhe.Length > 0 && !Recuo ? new Rectangle(x, 6, lt, 20) : new Rectangle(x, 0, lt, Height - 1);
                TextRenderer.DrawText(g, Text, f, rt, Recuo ? Tema.TextoSec : Tema.Texto,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            }
            if (Detalhe.Length > 0 && !Recuo)
                using (Font f = Tema.Fonte(8.5f))
                    TextRenderer.DrawText(g, Detalhe, f, new Rectangle(x, 26, lt, 18), Tema.TextoSec, TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            if (Acao != null)
                Icones.Desenhar(g, "seta", new RectangleF(Width - 22, (Height - 14) / 2f, 14, 14), _sobre ? Tema.Ciano : Tema.TextoFraco, 1.6f);
        }
    }

    /// <summary>Etiqueta arredondada (contadores, áreas das tags).</summary>
    internal class Etiqueta : Control
    {
        public Color CorFundo = Color.FromArgb(20, 60, 98);
        public Color CorTexto = Color.FromArgb(191, 233, 255);

        public Etiqueta(string texto)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            Font = Tema.Fonte(8f, FontStyle.Bold);
            Margin = new Padding(0, 0, 6, 0);
            Height = 22;
            Text = texto;
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Width = TextRenderer.MeasureText(Text ?? "", Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding).Width + 18;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bPai = new SolidBrush(Tema.FundoDe(this))) g.FillRectangle(bPai, ClientRectangle);
            using (GraphicsPath p = Tema.Arredondado(new Rectangle(0, 0, Width - 1, Height - 1), Height / 2 - 1))
            using (var b = new SolidBrush(CorFundo))
                g.FillPath(b, p);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, CorTexto, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Imagem embutida desenhada na proporção, centralizada (logo da tela inicial).</summary>
    internal class Figura : Control
    {
        public Image Imagem;

        public Figura(Image imagem)
        {
            Imagem = imagem;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Imagem == null || Width <= 0 || Height <= 0) return;
            float k = Math.Min(Width / (float)Imagem.Width, Height / (float)Imagem.Height);
            float w = Imagem.Width * k, h = Imagem.Height * k;
            e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.DrawImage(Imagem, (Width - w) / 2f, (Height - h) / 2f, w, h);
        }
    }

    /// <summary>Menus de contexto no tom do tema.</summary>
    internal class MenuEscuro : ToolStripProfessionalRenderer
    {
        private class Cores : ProfessionalColorTable
        {
            public override Color MenuItemSelected { get { return Tema.Selecao; } }
            public override Color MenuItemSelectedGradientBegin { get { return Tema.Selecao; } }
            public override Color MenuItemSelectedGradientEnd { get { return Tema.Selecao; } }
            public override Color MenuItemBorder { get { return Tema.Azul; } }
            public override Color MenuBorder { get { return Tema.BordaClara; } }
            public override Color ToolStripDropDownBackground { get { return Tema.Cartao; } }
            public override Color ImageMarginGradientBegin { get { return Tema.Cartao; } }
            public override Color ImageMarginGradientMiddle { get { return Tema.Cartao; } }
            public override Color ImageMarginGradientEnd { get { return Tema.Cartao; } }
            public override Color SeparatorDark { get { return Tema.Borda; } }
            public override Color SeparatorLight { get { return Tema.Borda; } }
        }

        public MenuEscuro() : base(new Cores()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Tema.Texto : Tema.TextoFraco;
            base.OnRenderItemText(e);
        }

        public static ContextMenuStrip Novo()
        {
            return new ContextMenuStrip { Renderer = new MenuEscuro(), BackColor = Tema.Cartao, ForeColor = Tema.Texto, ShowImageMargin = false, Font = Tema.Normal };
        }
    }

    // ===================================================================
    //  Caixas de mensagem no tema (no lugar das do Windows)
    // ===================================================================
    internal static class Dialogo
    {
        public static void Info(IWin32Window dono, string texto) { Mostrar(dono, "info", Tema.Ciano, App.Nome, texto, new[] { "OK" }, 0); }
        public static void Aviso(IWin32Window dono, string texto) { Mostrar(dono, "alerta", Tema.Amarelo, "Atenção", texto, new[] { "OK" }, 0); }
        public static void Erro(IWin32Window dono, string texto) { Mostrar(dono, "erro", Tema.Vermelho, "Não deu certo", texto, new[] { "OK" }, 0); }

        public static bool Confirmar(IWin32Window dono, string texto, string textoSim = "Continuar", string titulo = "Confirmar")
        {
            return Mostrar(dono, "pergunta", Tema.Ciano, titulo, texto, new[] { "Cancelar", textoSim }, 1) == 1;
        }

        /// <summary>Devolve o índice do botão escolhido (-1 se a janela foi fechada).</summary>
        public static int Mostrar(IWin32Window dono, string icone, Color cor, string titulo, string texto, string[] botoes, int primario)
        {
            int escolhido = -1;
            using (var f = new Form())
            {
                Tema.Janela(f);
                f.Text = App.Nome;
                f.FormBorderStyle = FormBorderStyle.FixedDialog;
                f.MaximizeBox = f.MinimizeBox = false;
                f.ShowInTaskbar = false;
                f.StartPosition = dono != null ? FormStartPosition.CenterParent : FormStartPosition.CenterScreen;

                const int larguraTexto = 440;
                var lblTitulo = Tema.TituloCartao(titulo);
                lblTitulo.Font = Tema.Fonte(13f, FontStyle.Bold);
                lblTitulo.Location = new Point(76, 22);
                var lblTexto = new Label { Text = texto, ForeColor = Tema.TextoSec, BackColor = Color.Transparent, Font = Tema.Fonte(9.75f), UseMnemonic = false };
                Tema.Medir(lblTexto, larguraTexto);
                lblTexto.Location = new Point(77, 56);

                var figura = new Panel { Location = new Point(24, 22), Size = new Size(40, 40), BackColor = Color.Transparent };
                figura.Paint += delegate (object s, PaintEventArgs e)
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var b = new SolidBrush(Color.FromArgb(36, cor))) e.Graphics.FillEllipse(b, 0, 0, 39, 39);
                    Icones.Desenhar(e.Graphics, icone, new RectangleF(9, 9, 22, 22), cor, 2f);
                };

                var barra = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 66, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(14, 14, 14, 0), BackColor = Tema.FundoEscuro };
                Button padrao = null, cancelar = null;
                for (int i = botoes.Length - 1; i >= 0; i--)
                {
                    int idx = i;
                    Button b = Tema.Botao(botoes[i], i == primario, delegate { escolhido = idx; f.Close(); });
                    b.MinimumSize = new Size(104, 38);
                    barra.Controls.Add(b);
                    if (i == primario) padrao = b;
                    if (i == 0 && botoes.Length > 1) cancelar = b;
                }
                f.ClientSize = new Size(76 + larguraTexto + 28, Math.Max(lblTexto.Bottom, 70) + 26 + barra.Height);
                f.Controls.Add(lblTitulo);
                f.Controls.Add(lblTexto);
                f.Controls.Add(figura);
                f.Controls.Add(barra);
                f.AcceptButton = padrao;
                if (cancelar != null) f.CancelButton = cancelar;
                else f.KeyPreview = true;
                f.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) f.Close(); };
                f.Shown += delegate { if (padrao != null) padrao.Focus(); };
                if (dono != null) f.ShowDialog(dono); else f.ShowDialog();
            }
            return escolhido;
        }
    }
}
