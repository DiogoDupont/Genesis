using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Genesis.Hardware;
using Genesis.Receitas;

namespace Genesis
{
    internal partial class MainForm : Form
    {
        private static readonly string[] TiposDados =
            { "Bool", "Byte", "Word", "DWord", "Int", "UInt", "DInt", "UDInt", "Real" };

        private const string Nenhum = "(nenhum)";

        // --- dados ------------------------------------------------------------------
        private readonly BindingList<Partida> _partidas = new BindingList<Partida>();
        private readonly BindingList<Partida> _sensores = new BindingList<Partida>();
        private readonly BindingList<Partida> _pids = new BindingList<Partida>();
        private Genesis.Hardware.ProjetoHardware _projetoHardware;
        private readonly BindingList<Io> _ios = new BindingList<Io>();
        private bool _carregando;
        private string _arquivoAtual;

        // --- tela -------------------------------------------------------------------
        private Panel _hostPaginas;
        private Control _pagInicio, _pagPartidas, _pagSensores, _pagPids, _pagTags, _pagConfig, _pagModbus, _pagMaquinas, _pagGerar, _pagHardware, _paginaAtual;
        private ItemNav _navInicio, _navPart, _navSen, _navPid, _navTags, _navModbus, _navMaq, _navGerar, _navHardware;
        private CartaoInicio _hubTags, _hubMaq, _hubPart, _hubSen, _hubPid, _hubGerar;
        private Label _lblProjetoInicio, _lblResumoInicio, _lblStatus, _lblAlternarLog;
        private Panel _gaveta;
        private FlowLayoutPanel _resumoTags;
        private readonly ToolTip _dicas = new ToolTip();
        private Timer _tmrContadores;
        private bool _modificado;
        private FlowLayoutPanel _listaMaquinas;
        private Control _pagMaquina;                     // edicao de uma maquina
        private Cadastro _cadMaq;
        private readonly BindingList<Partida> _itensMaq = new BindingList<Partida>();
        private string _maqAtual;
        private Label _maqTitulo, _maqResumo;
        private DataGridView _gridModbus;
        private FlowLayoutPanel _resumoModbus;
        private Label _avisoModbus;
        private TextBox _txtComDb, _txtComNum, _txtStatusWord, _txtIniStatus, _txtPrimeiroCiclo, _txtIniAux;
        private TextBox _txtMbInst, _txtMbCon, _txtMbIface, _txtMbPorta, _txtClock;
        private TextBox _txtPadraoConf, _txtPadraoHab, _txtPadraoHabInv, _txtPadraoHabSoft, _txtPadraoHabBomba1, _txtPadraoHabBomba2;
        private TextBox _txtPadraoDef, _txtPadraoMedidor, _txtPadraoTotalizador, _txtPadraoChave, _txtPadraoChaveMovimento;
        private CheckBox _chkCom, _chkComRecriar, _chkPidNoOb;
        private TextBox _txtObPid;
        private Cadastro _cadPart, _cadSen, _cadPid;
        private DataGridView _gridIos;
        private DataGridViewTextBoxColumn _colUsada;
        private ComboBox _cmbAreaPadrao;
        private TextBox _txtPrefixo, _txtReset, _txtEmerg, _txtRemLoc, _txtExport, _txtVersaoPid, _txtIniDef, _txtWordDef;
        private CheckBox _chkManter, _chkAlocarDef;
        private RichTextBox _txtLog;
        private TextBox _txtLocalizar;
        private Label _lblArquivo;
        private readonly List<Button> _botoesTopo = new List<Button>();

        // ===================================================================
        //  Cadastro: lista (Partidas, Sensores ou PIDs) + editor do item
        // ===================================================================
        private class Cadastro
        {
            public string Nome;                          // "partida", "sensor", "PID"
            public BindingList<Partida> Lista;
            public DataGridView Grade;
            public Panel Editor;
            public TextBox Busca;
            public ComboBox Filtro;   // Todas / Só pendentes / Avulsas / máquina
            public Label Situacao;    // resumo do item em edição (o que falta ou as opções ligadas)
            public Label Rodape;      // contagem embaixo da lista
            public Func<Partida, List<Secao>> Secoes;
            public Func<Partida> Novo;
            public Partida Editando;
            public bool Montando;
            public int LarguraMontada;
            public Dictionary<string, Control> Controles = new Dictionary<string, Control>();   // campo -> controle (editor atual)
        }

        private enum TipoCampo { Texto, Lista, Tag, Ref, Marcar, Acionamentos, Nota, Nome, TextoLongo }

        private class Campo
        {
            public string Prop, Rotulo, Ajuda;
            public TipoCampo Tipo;
            public Func<List<string>> Itens;
            public bool Obrigatorio;
            public Func<string, string> Info;            // Ref: texto explicando o valor escolhido
            public bool Destaque;                        // Nota em amarelo
            public string[] Props, Legendas;             // Nome: DB . grupo . nome numa linha
            // Acionamentos: quem pode ser ligado, se esta ligado e como ligar/desligar
            public Func<List<Partida>> Candidatos;
            public Func<Partida, bool> Ligado;
            public Action<Partida, bool> Ligar;
        }

        private class Secao
        {
            public string Titulo, Descricao;
            public List<Campo> Campos = new List<Campo>();
            public Secao(string titulo, string descricao) { Titulo = titulo; Descricao = descricao; }
        }

        // ===================================================================
        //  Construcao da janela
        // ===================================================================
        internal static MainForm Atual;

        public MainForm() : this(null, null) { }

        /// <param name="arquivoInicial">Projeto para abrir (ex.: duplo clique num .genesis); null = última sessão.</param>
        /// <param name="paginaInicial">Página para abrir direto (tags, maquinas, partidas, sensores, pids, modbus, gerar, config).</param>
        public MainForm(string arquivoInicial, string paginaInicial)
        {
            Atual = this;
            Tema.Janela(this);
            Text = App.Nome;
            Width = 1440;
            Height = 900;
            MinimumSize = new Size(1180, 720);
            StartPosition = FormStartPosition.CenterScreen;

            Cfg.Carregar();

            // de cima para baixo: barra do topo, páginas, log (recolhido) e linha de status
            _hostPaginas = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Fundo };
            Controls.Add(_hostPaginas);
            Controls.Add(CriarGaveta());
            Controls.Add(CriarBarraStatus());
            Controls.Add(CriarBarraTopo());

            _pagInicio = CriarPaginaInicio();
            _pagPartidas = CriarPaginaPartidas();
            _pagSensores = CriarPaginaSensores();
            _pagPids = CriarPaginaPids();
            _pagTags = CriarPaginaTags();
            _pagConfig = CriarPaginaConfig();
            _pagModbus = CriarPaginaModbus();
            _pagMaquinas = CriarPaginaMaquinas();
            _pagMaquina = CriarPaginaMaquina();
            _pagGerar = CriarPaginaGerar();
            _pagHardware = CriarPaginaHardware();
            foreach (Control p in new[] { _pagInicio, _pagPartidas, _pagSensores, _pagPids, _pagTags, _pagConfig, _pagModbus, _pagMaquinas, _pagMaquina, _pagGerar, _pagHardware })
            {
                p.Visible = false;
                _hostPaginas.Controls.Add(p);
            }

            Load += delegate
            {
                AoAbrir(arquivoInicial);
                Control inicial = PaginaPeloNome(paginaInicial);
                if (inicial != null) MostrarPagina(inicial);
            };
            FormClosing += delegate { AoFechar(); };

            // listas: contadores, editores que dependem de outras listas e "Usada em"
            _ios.ListChanged += delegate { if (!_carregando) AoMudarDados(); };
            _partidas.ListChanged += delegate { if (!_carregando) AoMudarDados(); };
            _sensores.ListChanged += delegate { if (!_carregando) AoMudarDados(); };
            _pids.ListChanged += delegate { if (!_carregando) AoMudarDados(); };

            _ios.AddingNew += delegate (object s, AddingNewEventArgs e)
            {
                string area = AreaPadrao();
                e.NewObject = new Io { Area = area, Tipo = Io.TipoPadrao(area, "", "") };
            };

            KeyPreview = true;
            KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.S) { e.Handled = e.SuppressKeyPress = true; Salvar(e.Shift); }
                else if (e.Control && e.KeyCode == Keys.O) { e.Handled = e.SuppressKeyPress = true; Abrir(); }
                else if (e.KeyCode == Keys.F5) { e.Handled = e.SuppressKeyPress = true; AbrirGerar(null); }
                else if (e.Control && !e.Shift && !e.Alt && e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D7)
                {
                    // Ctrl+1..6 = etapas na ordem da barra (Tags, Maquinas, Partidas, Sensores, PIDs, Modbus); Ctrl+7 = Configuracao; Ctrl+0 = Inicio
                    Control destino = null;
                    switch (e.KeyCode)
                    {
                        case Keys.D0: destino = _pagInicio; break;
                        case Keys.D1: destino = _pagTags; break;
                        case Keys.D2: destino = _pagMaquinas; break;
                        case Keys.D3: destino = _pagPartidas; break;
                        case Keys.D4: destino = _pagSensores; break;
                        case Keys.D5: destino = _pagPids; break;
                        case Keys.D6: destino = _pagModbus; break;
                        case Keys.D7: destino = _pagConfig; break;
                    }
                    if (destino != null) { e.Handled = e.SuppressKeyPress = true; MostrarPagina(destino); }
                }
            };

            MostrarPagina(_pagInicio);
        }

        private Control PaginaPeloNome(string nome)
        {
            switch ((nome ?? "").Trim().ToLowerInvariant())
            {
                case "tags": return _pagTags;
                case "maquinas": return _pagMaquinas;
                case "partidas": return _pagPartidas;
                case "sensores": return _pagSensores;
                case "pids": return _pagPids;
                case "modbus": return _pagModbus;
                case "hardware": return _pagHardware;
                case "gerar": return _pagGerar;
                case "config": return _pagConfig;
                default: return null;
            }
        }

        // --- barra do topo: marca, navegação e projeto ------------------------------
        private Control CriarBarraTopo()
        {
            var barra = new FundoDegrade { Dock = DockStyle.Top, Height = 58 };

            var marca = new Figura(Tema.Imagem("marca.png")) { Size = new Size(40, 40), Location = new Point(14, 9), Cursor = Cursors.Hand };
            var nome = new Label
            {
                Text = string.Join("\u2009", App.Nome.ToUpperInvariant().Select(c => c.ToString()).ToArray()),
                AutoSize = true,
                Font = Tema.Fonte(12f, FontStyle.Bold),
                ForeColor = Color.FromArgb(214, 238, 255),
                BackColor = Color.Transparent,
                Location = new Point(58, 17),
                Cursor = Cursors.Hand,
                UseMnemonic = false
            };
            marca.Click += delegate { MostrarPagina(_pagInicio); };
            nome.Click += delegate { MostrarPagina(_pagInicio); };
            barra.Controls.Add(marca);
            barra.Controls.Add(nome);

            _navInicio = new ItemNav("", "inicio");
            _navTags = new ItemNav("Tags", "tags");
            _navMaq = new ItemNav("Máquinas", "maquinas");
            _navPart = new ItemNav("Partidas", "partidas");
            _navSen = new ItemNav("Sensores", "sensores");
            _navPid = new ItemNav("PIDs", "pids");
            _navModbus = new ItemNav("Modbus", "modbus");
            _navGerar = new ItemNav("Gerar", "gerar") { Destaque = true };
            _navHardware = new ItemNav("Hardware", "config");
            _navInicio.Click += delegate { MostrarPagina(_pagInicio); };
            _navTags.Click += delegate { MostrarPagina(_pagTags); };
            _navMaq.Click += delegate { MostrarPagina(_pagMaquinas); };
            _navPart.Click += delegate { MostrarPagina(_pagPartidas); };
            _navSen.Click += delegate { MostrarPagina(_pagSensores); };
            _navPid.Click += delegate { MostrarPagina(_pagPids); };
            _navModbus.Click += delegate { MostrarPagina(_pagModbus); };
            _navGerar.Click += delegate { AbrirGerar(null); };
            _navHardware.Click += delegate { MostrarPagina(_pagHardware); };
            _dicas.SetToolTip(_navInicio, "Início");
            _dicas.SetToolTip(_navGerar, "Conferir e gerar no TIA Portal (F5)");

            var nav = new FlowLayoutPanel { Location = new Point(178, 0), Height = 58, AutoSize = true, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0) };
            foreach (ItemNav i in new[] { _navInicio, _navTags, _navMaq, _navHardware, _navPart, _navSen, _navPid, _navModbus, _navGerar })
            {
                i.Height = 58;
                nav.Controls.Add(i);
            }
            barra.Controls.Add(nav);

            // projeto: nome do arquivo e ações
            var acoes = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 11, 8, 0) };
            _lblArquivo = new Label
            {
                AutoSize = false,
                Size = new Size(210, 36),
                TextAlign = ContentAlignment.MiddleRight,
                AutoEllipsis = true,
                ForeColor = Tema.TextoSec,
                BackColor = Color.Transparent,
                Font = Tema.Pequena,
                Margin = new Padding(0, 0, 10, 0),
                Text = "Projeto sem nome",
                UseMnemonic = false
            };
            acoes.Controls.Add(_lblArquivo);

            var menu = MenuEscuro.Novo();
            menu.Items.Add("Salvar como…   (Ctrl+Shift+S)", null, delegate { Salvar(true); });
            menu.Items.Add("Importar CSV (ios.csv e partidas.csv)…", null, delegate { ImportarCsv(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Abrir a pasta dos dados do " + App.Nome, null, delegate { AbrirPastaApp(); });
            Button btnMenu = null;
            btnMenu = Tema.BotaoIcone("menu", "Mais opções", delegate { menu.Show(btnMenu, new Point(btnMenu.Width - menu.PreferredSize.Width, btnMenu.Height + 4)); }, _dicas);

            foreach (Button b in new[]
            {
                Tema.BotaoIcone("novo", "Novo projeto", delegate { Novo(); }, _dicas),
                Tema.BotaoIcone("abrir", "Abrir projeto  (Ctrl+O)", delegate { Abrir(); }, _dicas),
                Tema.BotaoIcone("salvar", "Salvar  (Ctrl+S)", delegate { Salvar(false); }, _dicas),
                btnMenu,
                Tema.BotaoIcone("config", "Configuração", delegate { MostrarPagina(_pagConfig); }, _dicas)
            })
            {
                acoes.Controls.Add(b);
                _botoesTopo.Add(b);
            }
            barra.Controls.Add(acoes);

            // janela estreita: o nome do arquivo cede lugar à navegação
            Action ajustar = delegate { _lblArquivo.Visible = barra.Width - nav.Right - 250 > _lblArquivo.Width; };
            barra.Resize += delegate { ajustar(); };
            nav.SizeChanged += delegate { ajustar(); };
            return barra;
        }

        // --- linha de status e log recolhível ---------------------------------------
        private Control CriarBarraStatus()
        {
            var barra = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = Tema.FundoEscuro };
            barra.Paint += delegate (object s, PaintEventArgs e)
            {
                using (var p = new Pen(Tema.Borda)) e.Graphics.DrawLine(p, 0, 0, barra.Width, 0);
            };
            _lblStatus = new Label
            {
                Dock = DockStyle.Fill,
                AutoSize = false,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(14, 1, 0, 0),
                ForeColor = Tema.TextoSec,
                BackColor = Color.Transparent,
                Font = Tema.Pequena,
                UseMnemonic = false,
                Cursor = Cursors.Hand
            };
            _lblAlternarLog = new Label
            {
                Dock = DockStyle.Right,
                AutoSize = false,
                Width = 96,
                TextAlign = ContentAlignment.MiddleCenter,
                Padding = new Padding(0, 1, 0, 0),
                ForeColor = Tema.Ciano,
                BackColor = Color.Transparent,
                Font = Tema.Fonte(8.5f, FontStyle.Bold),
                Text = "Ver log",
                Cursor = Cursors.Hand
            };
            var versao = new Label
            {
                Dock = DockStyle.Right,
                AutoSize = false,
                Width = 96,
                TextAlign = ContentAlignment.MiddleRight,
                Padding = new Padding(0, 1, 12, 0),
                ForeColor = Tema.TextoFraco,
                BackColor = Color.Transparent,
                Font = Tema.Pequena,
                Text = App.Nome + " " + App.Versao
            };
            _lblStatus.Click += delegate { MostrarLog(!_gaveta.Visible); };
            _lblAlternarLog.Click += delegate { MostrarLog(!_gaveta.Visible); };
            barra.Controls.Add(_lblStatus);
            barra.Controls.Add(_lblAlternarLog);
            barra.Controls.Add(versao);
            return barra;
        }

        private Control CriarGaveta()
        {
            _gaveta = new Panel { Dock = DockStyle.Bottom, Height = 164, Visible = false, BackColor = Tema.FundoEscuro, Padding = new Padding(16, 9, 10, 6) };
            _gaveta.Paint += delegate (object s, PaintEventArgs e)
            {
                using (var p = new Pen(Tema.Borda)) e.Graphics.DrawLine(p, 0, 0, _gaveta.Width, 0);
            };
            var topo = new Panel { Dock = DockStyle.Top, Height = 24, BackColor = Color.Transparent };
            var lbl = Tema.Secao("Log da geração");
            lbl.Location = new Point(0, 3);
            topo.Controls.Add(lbl);
            var limpar = new Label
            {
                Text = "limpar",
                Dock = DockStyle.Right,
                AutoSize = false,
                Width = 60,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Tema.TextoSec,
                BackColor = Color.Transparent,
                Font = Tema.Pequena,
                Cursor = Cursors.Hand
            };
            limpar.Click += delegate { _txtLog.Clear(); };
            topo.Controls.Add(limpar);
            var copiar = new Label
            {
                Text = "copiar",
                Dock = DockStyle.Right,
                AutoSize = false,
                Width = 60,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Tema.TextoSec,
                BackColor = Color.Transparent,
                Font = Tema.Pequena,
                Cursor = Cursors.Hand
            };
            copiar.Click += delegate
            {
                // copia o log inteiro (ou so o trecho selecionado) para a area de transferencia
                string texto = string.IsNullOrEmpty(_txtLog.SelectedText) ? _txtLog.Text : _txtLog.SelectedText;
                if (string.IsNullOrEmpty(texto)) return;
                try { Clipboard.SetText(texto); copiar.Text = "copiado ✓"; }
                catch (Exception) { copiar.Text = "não deu"; }
                var volta = new Timer { Interval = 1500 };
                volta.Tick += delegate { copiar.Text = "copiar"; volta.Stop(); volta.Dispose(); };
                volta.Start();
            };
            topo.Controls.Add(copiar);

            _txtLog = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Tema.FundoEscuro,
                ForeColor = Tema.Verde,
                Font = Tema.Mono,
                DetectUrls = false,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            Win.Escurecer(_txtLog);
            _gaveta.Controls.Add(_txtLog);
            _gaveta.Controls.Add(topo);
            return _gaveta;
        }

        private void MostrarLog(bool visivel)
        {
            if (_gaveta.Visible == visivel) return;
            _gaveta.Visible = visivel;
            _lblAlternarLog.Text = visivel ? "Ocultar log" : "Ver log";
            if (visivel) _txtLog.ScrollToCaret();
        }

        private void MostrarPagina(Control pagina)
        {
            if (_paginaAtual == pagina) return;
            foreach (Control c in _hostPaginas.Controls) c.Visible = c == pagina;
            _paginaAtual = pagina;
            _navInicio.Selecionado = pagina == _pagInicio;
            _navPart.Selecionado = pagina == _pagPartidas;
            _navSen.Selecionado = pagina == _pagSensores;
            _navPid.Selecionado = pagina == _pagPids;
            _navTags.Selecionado = pagina == _pagTags;
            _navModbus.Selecionado = pagina == _pagModbus;
            _navMaq.Selecionado = pagina == _pagMaquinas || pagina == _pagMaquina;
            _navGerar.Selecionado = pagina == _pagGerar;
            if (_navHardware != null) _navHardware.Selecionado = pagina == _pagHardware;
            // as listas principais mostram o que foi editado na pagina da maquina
            if (pagina == _pagPartidas || pagina == _pagSensores || pagina == _pagPids)
            {
                Cadastro c = pagina == _pagPartidas ? _cadPart : pagina == _pagSensores ? _cadSen : _cadPid;
                bool antes = _carregando;
                _carregando = true;   // só redesenha a lista: não conta como alteração do projeto
                try { ((BindingList<Partida>)c.Lista).ResetBindings(); }
                finally { _carregando = antes; }
                AplicarFiltro(c);
                MontarEditor(c, true);
            }
            if (pagina == _pagMaquinas) AtualizarMaquinas();
            if (pagina == _pagModbus) AtualizarModbus();
            if (pagina == _pagTags) AtualizarResumoTags();
            if (pagina == _pagGerar) AtualizarConferencia();
            if (pagina == _pagInicio) AtualizarContadoresAgora();
        }

        private Cadastro CadastroDaPagina(Control pagina)
        {
            if (pagina == _pagPartidas) return _cadPart;
            if (pagina == _pagSensores) return _cadSen;
            if (pagina == _pagPids) return _cadPid;
            return null;
        }

        /// <summary>Página padrão (como as telas do Orquestra): voltar, título, subtítulo, botões e conteúdo.</summary>
        private Panel NovaPagina(string titulo, string subtitulo, string descricao, Control[] botoes, Control conteudo,
            string rotuloVoltar = "Início", Action voltar = null)
        {
            var pag = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Fundo, Padding = new Padding(28, 16, 28, 12) };

            var topo = new Panel { Dock = DockStyle.Top, Height = 64, BackColor = Tema.Fundo };
            Button btnVoltar = Tema.Botao(rotuloVoltar, false, delegate { if (voltar != null) voltar(); else MostrarPagina(_pagInicio); }, "voltar");
            btnVoltar.Location = new Point(0, 7);
            topo.Controls.Add(btnVoltar);
            int x = btnVoltar.PreferredSize.Width + 16;
            var t = Tema.Titulo(titulo, 18f);
            t.Location = new Point(x, 0);
            var st = Tema.Subtitulo(subtitulo);
            st.Location = new Point(x + 3, 37);
            topo.Controls.Add(t);
            topo.Controls.Add(st);
            if (botoes != null && botoes.Length > 0)
            {
                var linha = new FlowLayoutPanel
                {
                    Dock = DockStyle.Right,
                    AutoSize = true,
                    WrapContents = false,
                    BackColor = Tema.Fundo,
                    Padding = new Padding(0, 7, 0, 0)
                };
                linha.Controls.AddRange(botoes);
                if (botoes.Length > 0) botoes[botoes.Length - 1].Margin = new Padding(0);
                topo.Controls.Add(linha);
                linha.BringToFront();
            }

            conteudo.Dock = DockStyle.Fill;
            pag.Controls.Add(conteudo);
            if (!string.IsNullOrEmpty(descricao))
            {
                var d = new Label
                {
                    Text = descricao,
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    ForeColor = Tema.TextoFraco,
                    BackColor = Tema.Fundo,
                    Font = Tema.Pequena,
                    Padding = new Padding(2, 2, 0, 0),
                    UseMnemonic = false
                };
                Action medir = delegate
                {
                    int larg = Math.Max(300, pag.ClientSize.Width - pag.Padding.Horizontal - 4);
                    d.Height = TextRenderer.MeasureText(d.Text, d.Font, new Size(larg, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl).Height + 16;
                };
                pag.Resize += delegate { medir(); };
                medir();
                pag.Controls.Add(d);
            }
            else pag.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8, BackColor = Tema.Fundo });
            pag.Controls.Add(topo);
            return pag;
        }

        // ===================================================================
        //  Início: logo, projeto e as etapas (cartões)
        // ===================================================================
        private Control CriarPaginaInicio()
        {
            var pag = new Panel { Dock = DockStyle.Fill, BackColor = Tema.Fundo, AutoScroll = true };
            Win.Escurecer(pag);
            var miolo = new Panel { BackColor = Tema.Fundo };

            var logo = new Figura(Tema.Imagem("logo.png")) { BackColor = Tema.Fundo };
            var slogan = new Label
            {
                Text = App.Slogan, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
                Font = Tema.Fonte(12.5f), ForeColor = Tema.Texto, BackColor = Tema.Fundo, UseMnemonic = false
            };
            var sub = new Label
            {
                Text = "Do escopo ao programa do CLP, direto no TIA Portal", AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
                Font = Tema.Fonte(9.5f), ForeColor = Tema.TextoSec, BackColor = Tema.Fundo, UseMnemonic = false
            };

            // faixa do projeto (como o "CLP configurado" do Orquestra)
            var faixa = new Cartao { Padding = new Padding(64, 0, 14, 0) };
            faixa.Paint += delegate (object s, PaintEventArgs e)
            {
                Icones.Desenhar(e.Graphics, "abrir", new RectangleF(22, (faixa.Height - 26) / 2f, 26, 26), Tema.Ciano, 2f);
            };
            var rotProjeto = Tema.Ajuda("Projeto");
            rotProjeto.Location = new Point(64, 12);
            _lblProjetoInicio = new Label
            {
                Text = "Projeto sem nome", AutoSize = true, Font = Tema.Fonte(13f, FontStyle.Bold), ForeColor = Color.White,
                BackColor = Color.Transparent, Location = new Point(62, 29), UseMnemonic = false
            };
            _lblResumoInicio = new Label
            {
                AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true,
                Font = Tema.Normal, ForeColor = Tema.TextoSec, BackColor = Color.Transparent, Padding = new Padding(0, 0, 16, 0), UseMnemonic = false
            };
            var botoesProjeto = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, BackColor = Color.Transparent, Padding = new Padding(0, 14, 0, 0) };
            botoesProjeto.Controls.Add(Tema.Botao("Novo", false, delegate { Novo(); }, "novo"));
            botoesProjeto.Controls.Add(Tema.Botao("Abrir", false, delegate { Abrir(); }, "abrir"));
            Button salvar = Tema.Botao("Salvar", false, delegate { Salvar(false); }, "salvar");
            salvar.Margin = new Padding(0);
            botoesProjeto.Controls.Add(salvar);
            faixa.Controls.Add(_lblResumoInicio);
            faixa.Controls.Add(botoesProjeto);
            faixa.Controls.Add(rotProjeto);
            faixa.Controls.Add(_lblProjetoInicio);
            rotProjeto.BringToFront();
            _lblProjetoInicio.BringToFront();

            Func<string, string, string, string, Action, CartaoInicio> novo = delegate (string etapa, string titulo, string desc, string icone, Action acao)
            {
                var c = new CartaoInicio(titulo, desc, icone) { Etapa = etapa, Margin = new Padding(0) };
                c.Click += delegate { acao(); };
                miolo.Controls.Add(c);
                return c;
            };
            _hubTags = novo("ETAPA 1", "Tags de PLC", "Cole as entradas e saídas do TIA Portal ou da planilha. É delas que as partidas e os sensores se alimentam.", "tags", delegate { MostrarPagina(_pagTags); });
            _hubMaq = novo("ETAPA 2", "Máquinas", "Escopos prontos, como preparador de polímero e flotador: criam todas as partidas e sensores de uma vez.", "maquinas", delegate { MostrarPagina(_pagMaquinas); });
            _hubPart = novo("ETAPA 3", "Partidas", "Motores, bombas, inversores e válvulas. Cada tipo mostra só os campos que usa, com explicação.", "partidas", delegate { MostrarPagina(_pagPartidas); });
            _hubSen = novo("ETAPA 4", "Sensores", "Níveis digitais e analógicos, analíticos, medidores de vazão e pás do raspador.", "sensores", delegate { MostrarPagina(_pagSensores); });
            _hubPid = novo("ETAPA 5", "PIDs", "PID_Compact modulando a velocidade de inversores e bombas. No PID você marca quem ele comanda.", "pids", delegate { MostrarPagina(_pagPids); });
            _hubGerar = novo("ETAPA 6", "Gerar no TIA Portal", "Confere o projeto, mostra o que será criado e gera os blocos no TIA Portal aberto.", "gerar", delegate { AbrirGerar(null); });
            _hubGerar.Destaque = true;
            var cartoes = new[] { _hubTags, _hubMaq, _hubPart, _hubSen, _hubPid, _hubGerar };

            var atalhos = new FlowLayoutPanel { AutoSize = true, WrapContents = false, BackColor = Tema.Fundo };
            atalhos.Controls.Add(Tema.Botao("Mapa Modbus", false, delegate { MostrarPagina(_pagModbus); }, "modbus"));
            Button cfg = Tema.Botao("Configuração", false, delegate { MostrarPagina(_pagConfig); }, "config");
            cfg.Margin = new Padding(0);
            atalhos.Controls.Add(cfg);

            miolo.Controls.Add(logo);
            miolo.Controls.Add(slogan);
            miolo.Controls.Add(sub);
            miolo.Controls.Add(faixa);
            miolo.Controls.Add(atalhos);
            pag.Controls.Add(miolo);

            Action arrumar = delegate
            {
                int altura = pag.ClientSize.Height;
                int larg = Math.Max(860, Math.Min(1120, pag.ClientSize.Width - 56));
                int altLogo = altura >= 740 ? 150 : altura >= 660 ? 118 : 92;
                int altCartao = altura >= 700 ? 172 : 158;
                int y = altura >= 740 ? 14 : 6;
                logo.SetBounds(0, y, larg, altLogo); y += altLogo + 4;
                slogan.SetBounds(0, y, larg, 26); y += 26;
                sub.SetBounds(0, y, larg, 20); y += 20 + 16;
                faixa.SetBounds(0, y, larg, 66); y += 66 + 16;
                const int vao = 16;
                int lc = (larg - 2 * vao) / 3;
                for (int i = 0; i < cartoes.Length; i++)
                    cartoes[i].SetBounds((i % 3) * (lc + vao), y + (i / 3) * (altCartao + vao), i % 3 == 2 ? larg - 2 * (lc + vao) : lc, altCartao);
                y += 2 * altCartao + vao + 16;
                atalhos.Location = new Point((larg - atalhos.PreferredSize.Width) / 2, y); y += 38 + 14;
                miolo.SetBounds(Math.Max(28, (pag.ClientSize.Width - larg) / 2) + pag.AutoScrollPosition.X, pag.AutoScrollPosition.Y, larg, y);
            };
            pag.Resize += delegate { arrumar(); };
            pag.VisibleChanged += delegate { if (pag.Visible) arrumar(); };
            arrumar();
            return pag;
        }

        /// <summary>Pede a atualização dos contadores (junta várias mudanças seguidas numa só).</summary>
        private void AtualizarContadores()
        {
            if (_tmrContadores == null)
            {
                _tmrContadores = new Timer { Interval = 180 };
                _tmrContadores.Tick += delegate { _tmrContadores.Stop(); AtualizarContadoresAgora(); };
            }
            _tmrContadores.Stop();
            _tmrContadores.Start();
        }

        private static string Plural(int n, string um, string varios)
        {
            return n + " " + (n == 1 ? um : varios);
        }

        private int Pendentes(Cadastro cad)
        {
            return cad.Lista.Count(x => !PartidaVazia(x) && Resumo(cad, x).StartsWith("⚠"));
        }

        private void AtualizarContadoresAgora()
        {
            if (_navPart == null || _hubGerar == null || _cadPid == null) return;
            int nPart = _partidas.Count(x => !PartidaVazia(x));
            int nSen = _sensores.Count(x => !PartidaVazia(x));
            int nPid = _pids.Count(x => !PartidaVazia(x));
            int nTag = _ios.Count(x => !IoVazia(x));
            int pPart = Pendentes(_cadPart), pSen = Pendentes(_cadSen), pPid = Pendentes(_cadPid);
            int semEnd = _ios.Count(x => !Vazio(x.Nome) && Vazio(x.Endereco));
            int nMaq = Maquinas.MaquinasNoProjeto(_partidas.Concat(_sensores).Concat(_pids).ToList()).Count;

            _navTags.Definir(nTag > 0 ? nTag.ToString() : "", semEnd > 0);
            _navMaq.Definir(nMaq > 0 ? nMaq.ToString() : "", false);
            _navPart.Definir(nPart > 0 ? nPart.ToString() : "", pPart > 0);
            _navSen.Definir(nSen > 0 ? nSen.ToString() : "", pSen > 0);
            _navPid.Definir(nPid > 0 ? nPid.ToString() : "", pPid > 0);
            _dicas.SetToolTip(_navTags, semEnd > 0 ? Plural(semEnd, "tag sem endereço", "tags sem endereço") : "");
            _dicas.SetToolTip(_navPart, pPart > 0 ? Plural(pPart, "partida com campos a preencher", "partidas com campos a preencher") : "");
            _dicas.SetToolTip(_navSen, pSen > 0 ? Plural(pSen, "sensor com campos a preencher", "sensores com campos a preencher") : "");
            _dicas.SetToolTip(_navPid, pPid > 0 ? Plural(pPid, "PID com campos a preencher", "PIDs com campos a preencher") : "");

            Func<int, int, string, string, string> situacao = delegate (int n, int np, string um, string varios)
            {
                if (n == 0) return "Nenhum cadastrado";
                return Plural(n, um, varios) + (np > 0 ? "  ·  ⚠ " + np + (np == 1 ? " pendente" : " pendentes") : "  ·  ✓ completos");
            };
            _hubTags.Definir(nTag == 0 ? "Nenhuma tag ainda" : Plural(nTag, "tag", "tags") + (semEnd > 0 ? "  ·  ⚠ " + semEnd + " sem endereço" : ""),
                nTag == 0 ? Tema.TextoFraco : semEnd > 0 ? Tema.Amarelo : Tema.Ciano);
            _hubMaq.Definir(nMaq == 0 ? "Nenhuma no projeto" : Plural(nMaq, "máquina no projeto", "máquinas no projeto"), nMaq == 0 ? Tema.TextoFraco : Tema.Ciano);
            _hubPart.Definir(situacao(nPart, pPart, "partida", "partidas"), nPart == 0 ? Tema.TextoFraco : pPart > 0 ? Tema.Amarelo : Tema.Ciano);
            _hubSen.Definir(situacao(nSen, pSen, "sensor", "sensores"), nSen == 0 ? Tema.TextoFraco : pSen > 0 ? Tema.Amarelo : Tema.Ciano);
            _hubPid.Definir(situacao(nPid, pPid, "PID", "PIDs"), nPid == 0 ? Tema.TextoFraco : pPid > 0 ? Tema.Amarelo : Tema.Ciano);

            int total = nPart + nSen + nPid, pend = pPart + pSen + pPid;
            if (total == 0) _hubGerar.Definir("Cadastre algo para começar", Color.FromArgb(190, 215, 240));
            else if (pend > 0) _hubGerar.Definir("⚠ " + Plural(pend, "item com campos a preencher", "itens com campos a preencher"), Tema.Amarelo);
            else _hubGerar.Definir("✓ Pronto para conferir e gerar", Color.FromArgb(150, 245, 200));
            _navGerar.Definir("", false);

            _lblResumoInicio.Text = total + nTag == 0 ? "Projeto vazio: comece pela etapa 1 ou por uma máquina do catálogo"
                : Plural(nTag, "tag", "tags") + "   ·   " + Plural(nPart, "partida", "partidas") + "   ·   "
                    + Plural(nSen, "sensor", "sensores") + "   ·   " + Plural(nPid, "PID", "PIDs");
            foreach (Cadastro c in new[] { _cadPart, _cadSen, _cadPid, _cadMaq }) AtualizarRodapeLista(c);
            if (_paginaAtual == _pagGerar) AtualizarConferencia();
        }

        // ===================================================================
        //  Paginas de cadastro (Partidas, Sensores, PIDs)
        // ===================================================================
        private Control CriarPaginaPartidas()
        {
            _cadPart = new Cadastro
            {
                Nome = "partida",
                Lista = _partidas,
                Secoes = SecoesPartida,
                Novo = delegate
                {
                    Partida ult = _cadPart.Editando ?? _partidas.LastOrDefault();
                    return new Partida
                    {
                        Sub = ult != null ? ult.Sub : "",
                        Eq = ult != null ? ult.Eq : "",
                        Tipo = ult != null ? ult.Tipo : Gerador.NomesTiposPartida[0],
                        Auto = ult != null && ult.Auto,
                        Intertravamento = ult != null && ult.Intertravamento
                    };
                }
            };
            Control conteudo = CriarCadastro(_cadPart,
                new DataGridViewColumn[] { Col("Sub", "SUBSIST.", 70), Col("Eq", "EQUIP.", 62), Col("Fn", "FUNÇÃO", 62),
                        ColTipo(Gerador.NomesTiposPartida, 190), Col("Descricao", "DESCRIÇÃO", 170) });

            return NovaPagina("Partidas", "Motores · bombas · inversores · válvulas",
                "Escolha uma partida na lista para editar à direita. O editor mostra só os campos do tipo escolhido; os marcados com • são obrigatórios "
                + "e a coluna RESUMO avisa o que falta. Marque GERAR nos itens que quiser gerar separadamente.",
                new Control[]
                {
                    Tema.Botao("Nova partida", true, delegate { NovoItem(_cadPart); }, "mais"),
                    Tema.Botao("Em lote", false, delegate { AdicionarLote(); }, "lote"),
                    Tema.Botao("Duplicar", false, delegate { Duplicar(_cadPart); }, "duplicar"),
                    BotaoExcluir()
                }, conteudo);
        }

        private Control CriarPaginaSensores()
        {
            _cadSen = new Cadastro
            {
                Nome = "sensor",
                Lista = _sensores,
                Secoes = SecoesSensor,
                Novo = delegate
                {
                    Partida ult = _cadSen.Editando ?? _sensores.LastOrDefault();
                    return new Partida
                    {
                        Sub = ult != null ? ult.Sub : "SEN",
                        Eq = ult != null ? ult.Eq : "",
                        Tipo = ult != null ? ult.Tipo : Gerador.NomesTiposSensor[0]
                    };
                }
            };
            Control conteudo = CriarCadastro(_cadSen,
                new DataGridViewColumn[] { Col("Sub", "DB", 60), Col("Eq", "GRUPO", 70), Col("Fn", "SENSOR", 70),
                        ColTipo(Gerador.NomesTiposSensor, 210), Col("Descricao", "DESCRIÇÃO", 170) });

            return NovaPagina("Sensores", "Níveis · analíticos · vazão · pás do raspador",
                "Cada sensor vira um membro no DB (normalmente SEN) e uma rede na FC SENSORES. Nível alto e baixo digitais do mesmo grupo "
                + "dividem um BL_SENSOR_NIVEL_DIG. Medidores com modulação: marque no próprio medidor as bombas que ele comanda.",
                new Control[]
                {
                    Tema.Botao("Novo sensor", true, delegate { NovoItem(_cadSen); }, "mais"),
                    Tema.Botao("Duplicar", false, delegate { Duplicar(_cadSen); }, "duplicar"),
                    BotaoExcluir()
                }, conteudo);
        }

        private Control CriarPaginaPids()
        {
            _cadPid = new Cadastro
            {
                Nome = "PID",
                Lista = _pids,
                Secoes = SecoesPid,
                Novo = delegate
                {
                    Partida ult = _cadPid.Editando ?? _pids.LastOrDefault();
                    return new Partida { Sub = ult != null ? ult.Sub : "", Eq = "", Fn = "PID", Tipo = "PID" };
                }
            };
            Control conteudo = CriarCadastro(_cadPid,
                new DataGridViewColumn[] { Col("Sub", "DB", 60), Col("Eq", "GRUPO", 70), Col("Fn", "NOME", 60),
                        Col("Descricao", "DESCRIÇÃO", 190), Col("TagSensor", "SENSOR", 120) });

            return NovaPagina("PIDs", "PID_Compact · modulação de velocidade",
                "Cada PID vira um struct UDT_PID no DB, um objeto tecnológico PID_Compact e uma rede no OB de interrupção cíclica (OB30). "
                + "(Na Configuração dá para trocar por uma FC G_PID_<DB> para chamar no OB.) No editor, marque os acionamentos que o PID comanda (vários podem usar o mesmo).",
                new Control[]
                {
                    Tema.Botao("Novo PID", true, delegate { NovoItem(_cadPid); }, "mais"),
                    Tema.Botao("Duplicar", false, delegate { Duplicar(_cadPid); }, "duplicar"),
                    BotaoExcluir()
                }, conteudo);
        }

        private static DataGridViewTextBoxColumn Col(string prop, string titulo, int peso)
        {
            return new DataGridViewTextBoxColumn
            {
                Name = prop,
                DataPropertyName = prop,
                HeaderText = titulo,
                FillWeight = peso,
                MinimumWidth = Math.Min(90, 14 + 8 * titulo.Length),
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
        }

        private static DataGridViewComboBoxColumn ColTipo(string[] tipos, int peso)
        {
            var c = new DataGridViewComboBoxColumn
            {
                Name = "Tipo",
                DataPropertyName = "Tipo",
                HeaderText = "TIPO",
                FillWeight = peso,
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            c.Items.AddRange(tipos);
            return c;
        }

        /// <summary>Lista (com busca) a esquerda e editor a direita.</summary>
        private Control CriarCadastro(Cadastro cad, DataGridViewColumn[] colunas)
        {
            var split = new SplitContainer
            {
                Orientation = Orientation.Vertical,
                BackColor = Tema.Fundo,
                SplitterWidth = 14,
                FixedPanel = FixedPanel.Panel2
            };
            split.Panel1.BackColor = Tema.Fundo;
            split.Panel2.BackColor = Tema.Fundo;

            // --- lista ---
            var cartaoLista = new Cartao { Dock = DockStyle.Fill, Padding = new Padding(16, 14, 16, 10) };
            var busca = new TableLayoutPanel { Dock = DockStyle.Top, Height = 70, ColumnCount = 2, RowCount = 2, BackColor = Color.Transparent };
            busca.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            busca.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            busca.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
            busca.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            busca.Controls.Add(Tema.Secao("Buscar"), 0, 0);
            Label rotFiltro = Tema.Secao("Mostrar");
            rotFiltro.Margin = new Padding(10, 0, 0, 5);
            busca.Controls.Add(rotFiltro, 1, 0);
            cad.Busca = Tema.CaixaTexto();
            Control quadroBusca = Tema.Emoldurar(cad.Busca);
            quadroBusca.Dock = DockStyle.Top;
            quadroBusca.Margin = new Padding(0);
            busca.Controls.Add(quadroBusca, 0, 1);
            cad.Filtro = Tema.Lista(false);
            cad.Filtro.Items.AddRange(new object[] { FiltroTodas, FiltroPendentes, FiltroAvulsas });
            cad.Filtro.SelectedIndex = 0;
            cad.Filtro.SelectedIndexChanged += delegate { AplicarFiltro(cad); };
            Control quadroFiltro = Tema.Emoldurar(cad.Filtro);
            quadroFiltro.Dock = DockStyle.Top;
            quadroFiltro.Margin = new Padding(10, 0, 0, 0);
            busca.Controls.Add(quadroFiltro, 1, 1);
            cad.Busca.TextChanged += delegate { AplicarFiltro(cad); };
            cad.Busca.HandleCreated += delegate { DicaNaCaixa(cad.Busca, "Digite para filtrar (nome, tipo, descrição...)"); };

            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2
            };
            Tema.Grade(g);
            g.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            var colGerar = new DataGridViewCheckBoxColumn
            {
                Name = "Marcada",
                DataPropertyName = "Marcada",
                HeaderText = "GERAR",
                FillWeight = 40,
                MinimumWidth = 52,
                ToolTipText = "Marque para \"Gerar marcadas\". Clique no cabeçalho para marcar/desmarcar todos.",
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            g.Columns.Add(colGerar);
            g.Columns.AddRange(colunas);
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Resumo",
                HeaderText = "RESUMO",
                ReadOnly = true,
                FillWeight = 200,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
            g.DataSource = cad.Lista;
            g.DataError += delegate (object s, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
            cad.Grade = g;

            g.CellFormatting += delegate (object s, DataGridViewCellFormattingEventArgs e)
            {
                if (e.RowIndex < 0 || e.RowIndex >= cad.Lista.Count) return;
                if (g.Columns[e.ColumnIndex].Name != "Resumo") return;
                string r = Resumo(cad, cad.Lista[e.RowIndex]);
                e.Value = r;
                e.FormattingApplied = true;
                e.CellStyle.ForeColor = r.StartsWith("⚠") ? Tema.Amarelo : Tema.TextoSec;
                e.CellStyle.SelectionForeColor = r.StartsWith("⚠") ? Tema.Amarelo : Color.White;
            };
            g.CurrentCellDirtyStateChanged += delegate
            {
                if (g.IsCurrentCellDirty && g.CurrentCell is DataGridViewCheckBoxCell) g.CommitEdit(DataGridViewDataErrorContexts.Commit);
                else if (g.IsCurrentCellDirty && g.CurrentCell is DataGridViewComboBoxCell) g.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            g.ColumnHeaderMouseClick += delegate (object s, DataGridViewCellMouseEventArgs e)
            {
                if (e.ColumnIndex < 0 || g.Columns[e.ColumnIndex].Name != "Marcada") return;
                g.EndEdit();
                bool marcar = cad.Lista.Any(x => !x.Marcada);
                foreach (Partida p in cad.Lista) p.Marcada = marcar;
                g.Invalidate();
            };
            g.SelectionChanged += delegate { if (!cad.Montando) MontarEditor(cad, false); };
            g.CellValueChanged += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (cad.Montando || e.RowIndex < 0 || e.RowIndex >= cad.Lista.Count) return;
                if (cad.Lista[e.RowIndex] == cad.Editando && g.Columns[e.ColumnIndex].Name != "Marcada")
                    BeginInvoke(new Action(delegate { MontarEditor(cad, true); }));
                AtualizarContadores();
            };
            g.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Delete && !g.IsCurrentCellInEditMode) { e.Handled = true; ExcluirLinhas(); }
            };
            AdicionarMenuExcluir(g);

            var dicaLista = new Label
            {
                Dock = DockStyle.Bottom, AutoSize = false, Height = 28, TextAlign = ContentAlignment.BottomLeft, AutoEllipsis = true,
                ForeColor = Tema.TextoFraco, BackColor = Color.Transparent, Font = Tema.Pequena, UseMnemonic = false
            };
            cad.Rodape = dicaLista;

            cartaoLista.Controls.Add(g);
            cartaoLista.Controls.Add(busca);
            cartaoLista.Controls.Add(dicaLista);
            split.Panel1.Controls.Add(cartaoLista);

            // --- editor ---
            cad.Editor = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Tema.Fundo, Padding = new Padding(0, 0, 6, 0) };
            Win.Escurecer(cad.Editor);
            split.Panel2.Controls.Add(cad.Editor);

            Action ajustar = delegate
            {
                if (split.Width < 900) return;
                int editor = Math.Max(470, Math.Min(620, (int)(split.Width * 0.42)));
                split.SplitterDistance = split.Width - editor - split.SplitterWidth;
            };
            split.HandleCreated += delegate { split.Panel2MinSize = 440; ajustar(); };
            split.SizeChanged += delegate { ajustar(); };

            // o editor quebra os textos pela largura: remonta quando ela muda bastante
            cad.Editor.Resize += delegate
            {
                if (Math.Abs(cad.Editor.ClientSize.Width - cad.LarguraMontada) > 24 && cad.Editando != null)
                    BeginInvoke(new Action(delegate { MontarEditor(cad, true); }));
            };
            return split;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        /// <summary>Texto de dica dentro da caixa vazia (Windows).</summary>
        private static void DicaNaCaixa(TextBox tb, string texto)
        {
            try { SendMessage(tb.Handle, 0x1501, (IntPtr)1, texto); }   // EM_SETCUEBANNER
            catch { /* fora do Windows: sem dica */ }
        }

        private const string FiltroTodas = "Todas", FiltroPendentes = "Só pendentes (⚠)", FiltroAvulsas = "Avulsas (fora de máquina)";

        private static string RotuloMaquina(string id, string receita)
        {
            return "Máquina " + id + (string.IsNullOrEmpty(receita) ? "" : " · " + receita);
        }

        private static string RotuloParaMaquina(string rotulo)
        {
            if (!rotulo.StartsWith("Máquina ")) return rotulo;
            string r = rotulo.Substring(8);
            int i = r.IndexOf(" · ");
            return i >= 0 ? r.Substring(0, i) : r;
        }

        /// <summary>Atualiza a lista de maquinas nos filtros "Mostrar" (mantendo a escolha).</summary>
        private void AtualizarFiltrosMaquina()
        {
            var todas = _partidas.Concat(_sensores).Concat(_pids).ToList();
            var rotulos = Maquinas.MaquinasNoProjeto(todas)
                .Select(id => {
                    string rec = todas.FirstOrDefault(x => (string.Equals(x.Maquina, id, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Subsistema, id, StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrEmpty(x.Receita))?.Receita;
                    return RotuloMaquina(id, rec ?? Maquinas.Preparador);
                })
                .ToList();
            foreach (Cadastro c in new[] { _cadPart, _cadSen, _cadPid })
            {
                if (c == null || c.Filtro == null) continue;
                string atual = Convert.ToString(c.Filtro.SelectedItem);
                var itens = new List<object> { FiltroTodas, FiltroPendentes, FiltroAvulsas };
                itens.AddRange(rotulos);
                if (c.Filtro.Items.Cast<object>().Select(Convert.ToString).SequenceEqual(itens.Select(Convert.ToString))) continue;
                c.Filtro.Items.Clear();
                c.Filtro.Items.AddRange(itens.ToArray());
                int k = itens.Select(Convert.ToString).ToList().IndexOf(atual);
                c.Filtro.SelectedIndex = k >= 0 ? k : 0;
            }
        }

        /// <summary>Abre a pagina da maquina (todos os itens dela, editor, gerar so ela).</summary>
        private void EditarMaquina(string id)
        {
            AbrirMaquina(id);
        }

        /// <summary>(antigo) Abre as abas ja filtradas numa maquina.</summary>
        private void FiltrarMaquina(string id)
        {
            AtualizarFiltrosMaquina();
            foreach (Cadastro c in new[] { _cadPart, _cadSen, _cadPid })
            {
                if (c == null || c.Filtro == null) continue;
                foreach (object it in c.Filtro.Items)
                    if (RotuloParaMaquina(Convert.ToString(it)) == id) { c.Filtro.SelectedItem = it; break; }
            }
            MostrarPagina(_pagPartidas);
            Log("Mostrando só a máquina " + id + " (troque em \"Mostrar\" para ver tudo ou as avulsas).");
        }

        private void AplicarFiltro(Cadastro cad)
        {
            string f = (cad.Busca.Text ?? "").Trim().ToLowerInvariant();
            string maq = cad.Filtro != null ? Convert.ToString(cad.Filtro.SelectedItem) : FiltroTodas;
            DataGridView g = cad.Grade;
            int n = Math.Min(g.Rows.Count, cad.Lista.Count);
            var ver = new bool[n];
            for (int i = 0; i < n; i++)
            {
                Partida p = cad.Lista[i];
                string texto = (p.Sub + " " + p.Eq + " " + p.Fn + " " + p.Tipo + " " + p.Descricao + " " + p.TagSensor).ToLowerInvariant();
                bool v = f.Length == 0 || texto.Contains(f);
                if (maq == FiltroPendentes) v = v && Resumo(cad, p).StartsWith("⚠");
                else if (maq == FiltroAvulsas) v = v && string.IsNullOrEmpty(p.Maquina);
                else if (!string.IsNullOrEmpty(maq) && maq != FiltroTodas) 
                {
                    string idMaq = RotuloParaMaquina(maq);
                    v = v && (string.Equals(p.Maquina, idMaq, StringComparison.OrdinalIgnoreCase) || string.Equals(p.Subsistema, idMaq, StringComparison.OrdinalIgnoreCase));
                }
                ver[i] = v;
            }

            // a linha atual nao pode ser escondida: a selecao vai antes para a primeira que fica visivel
            int atual = g.CurrentCell != null ? g.CurrentCell.RowIndex : -1;
            if (atual >= 0 && atual < n && !ver[atual])
            {
                int prim = Array.IndexOf(ver, true);
                DataGridViewColumn col = g.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.Visible);
                if (prim >= 0 && col != null)
                {
                    g.Rows[prim].Visible = true;
                    g.CurrentCell = g.Rows[prim].Cells[col.Index];
                }
                else g.CurrentCell = null;
            }

            CurrencyManager cm = (CurrencyManager)BindingContext[g.DataSource];
            cm.SuspendBinding();
            try
            {
                for (int i = 0; i < n; i++)
                {
                    if (g.CurrentCell != null && g.CurrentCell.RowIndex == i && !ver[i]) continue;   // seguranca
                    try { g.Rows[i].Visible = ver[i]; }
                    catch (InvalidOperationException) { /* linha presa a posicao da lista (Mono): fica visivel */ }
                }
            }
            finally
            {
                cm.ResumeBinding();
            }
            AtualizarRodapeLista(cad);
        }

        /// <summary>Linha embaixo da lista: quantos itens, quantos pendentes e a dica de edição.</summary>
        private void AtualizarRodapeLista(Cadastro cad)
        {
            if (cad == null || cad.Rodape == null) return;
            int n = cad.Lista.Count(x => !PartidaVazia(x));
            int pend = cad.Lista.Count(x => !PartidaVazia(x) && Resumo(cad, x).StartsWith("⚠"));
            int visiveis = 0;
            for (int i = 0; i < cad.Grade.Rows.Count; i++) if (cad.Grade.Rows[i].Visible) visiveis++;
            string t = n == 0 ? "Lista vazia"
                : Plural(n, "item", "itens") + (visiveis < cad.Grade.Rows.Count ? " (" + visiveis + " na tela)" : "")
                    + (pend > 0 ? "   ·   ⚠ " + pend + (pend == 1 ? " pendente" : " pendentes") : "   ·   ✓ sem pendências");
            cad.Rodape.Text = t + "      F2 ou digitar edita na lista · clique direito exclui";
            cad.Rodape.ForeColor = pend > 0 ? Tema.Amarelo : Tema.TextoFraco;
        }

        private Button BotaoExcluir()
        {
            var b = (BotaoTema)Tema.Botao("Excluir", false, delegate { ExcluirLinhas(); }, "lixeira");
            b.Perigo = true;
            return b;
        }

        private Partida ItemAtual(Cadastro cad)
        {
            DataGridViewRow r = cad.Grade.CurrentRow;
            if (r == null || r.Index < 0 || r.Index >= cad.Lista.Count) return null;
            return cad.Lista[r.Index];
        }

        private void Selecionar(Cadastro cad, Partida p)
        {
            int i = cad.Lista.IndexOf(p);
            if (i < 0 || i >= cad.Grade.Rows.Count) return;
            if (!cad.Grade.Rows[i].Visible) { cad.Busca.Text = ""; }
            cad.Grade.ClearSelection();
            cad.Grade.CurrentCell = cad.Grade.Rows[i].Cells["Sub"];
            cad.Grade.Rows[i].Selected = true;
        }

        private void NovoItem(Cadastro cad)
        {
            Partida p = cad.Novo();
            cad.Lista.Add(p);
            Selecionar(cad, p);
            MontarEditor(cad, true);
            FocarPrimeiroCampo(cad);
        }

        private void Duplicar(Cadastro cad)
        {
            Partida orig = ItemAtual(cad);
            if (orig == null) return;
            var copia = new Partida();
            foreach (PropertyInfo pi in typeof(Partida).GetProperties())
            {
                if (!pi.CanWrite || !pi.CanRead) continue;
                if (pi.GetCustomAttributes(typeof(System.Xml.Serialization.XmlIgnoreAttribute), true).Length > 0) continue;
                pi.SetValue(copia, pi.GetValue(orig, null), null);
            }
            copia.Fn = (orig.Fn ?? "") + "_2";
            if (copia.TagDef == AutoDef(orig)) copia.TagDef = AutoDef(copia);
            if (copia.TagProtecaoMem == AutoPrt(orig)) copia.TagProtecaoMem = AutoPrt(copia);
            PreencherAutomaticos(copia);
            cad.Lista.Insert(cad.Lista.IndexOf(orig) + 1, copia);
            Selecionar(cad, copia);
            MontarEditor(cad, true);
            Log("Duplicado: " + orig.Sub + "." + orig.Eq + "." + orig.Fn + " → " + copia.Fn + " (ajuste o nome e as tags).");
        }

        private void FocarPrimeiroCampo(Cadastro cad)
        {
            foreach (Control c in Descendentes(cad.Editor))
            {
                if (c is TextBox) { c.Focus(); ((TextBox)c).SelectAll(); return; }
            }
        }

        private static IEnumerable<Control> Descendentes(Control raiz)
        {
            foreach (Control c in raiz.Controls)
            {
                yield return c;
                foreach (Control d in Descendentes(c)) yield return d;
            }
        }

        // ===================================================================
        //  Editor do item selecionado
        // ===================================================================
        private void MontarEditor(Cadastro cad, bool forcar, string focar = null)
        {
            if (cad == null || cad.Editor == null) return;
            Partida p = ItemAtual(cad);
            if (p != null) PreencherAutomaticos(p);
            if (!forcar && p == cad.Editando) return;
            cad.Editando = p;
            cad.Controles.Clear();

            Panel host = cad.Editor;
            cad.LarguraMontada = host.ClientSize.Width;
            // largura útil dos textos = largura - padding do cartão - barra de rolagem - folga
            _larguraTexto = Math.Max(240, host.ClientSize.Width - 44 - SystemInformation.VerticalScrollBarWidth - 14);
            cad.Situacao = null;
            host.SuspendLayout();
            int rolagem = forcar ? -host.AutoScrollPosition.Y : 0;
            foreach (Control c in host.Controls.Cast<Control>().ToList()) c.Dispose();
            host.Controls.Clear();

            var coluna = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 1,
                BackColor = Tema.Fundo,
                Padding = new Padding(0)
            };
            coluna.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            if (p == null)
            {
                var vazio = new CartaoTabela(1) { Dock = DockStyle.Fill };
                var t = Tema.TituloCartao(cad.Lista.Count == 0 ? "Nada cadastrado ainda" : "Nenhum item selecionado");
                t.Margin = new Padding(0, 2, 0, 6);
                vazio.Controls.Add(t);
                string texto = cad.Lista.Count == 0
                    ? (cad == _cadMaq ? "Esta máquina ainda não tem itens. Use \"Novo item\" ou traga itens avulsos."
                        : "Use o botão azul acima para cadastrar o primeiro(a) " + cad.Nome + "."
                            + (cad == _cadPid ? "" : " Se o escopo for uma máquina do catálogo (preparador, flotador), crie por lá: ela já vem com tudo ligado."))
                    : "Escolha um item na lista à esquerda para editar aqui.";
                var a = Tema.Ajuda(texto, _larguraTexto);
                a.Margin = new Padding(0, 0, 0, 6);
                vazio.Controls.Add(a);
                if (cad.Lista.Count == 0 && cad != _cadMaq && cad != _cadPid)
                {
                    Button ir = Tema.Botao("Ver o catálogo de máquinas", false, delegate { MostrarPagina(_pagMaquinas); }, "maquinas");
                    ir.Margin = new Padding(0, 6, 0, 4);
                    vazio.Controls.Add(ir);
                }
                coluna.Controls.Add(vazio);
            }
            else
            {
                // título do item e situação (o que falta ou as opções ligadas)
                var cab = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Tema.Fundo, Margin = new Padding(2, 0, 0, 10) };
                string id = (Vazio(p.Sub) ? "?" : p.Sub) + "." + (Vazio(p.Eq) ? "?" : p.Eq) + "." + (Vazio(p.Fn) ? "?" : p.Fn);
                cab.Controls.Add(Tema.Titulo(id, 15f));
                var st = Tema.Subtitulo(Vazio(p.Tipo) ? cad.Nome : p.Tipo);
                st.Margin = new Padding(3, 0, 0, 0);
                cab.Controls.Add(st);
                cad.Situacao = new Label { AutoSize = false, Font = Tema.Fonte(8.75f, FontStyle.Bold), BackColor = Tema.Fundo, Margin = new Padding(3, 5, 0, 0), UseMnemonic = false };
                cab.Controls.Add(cad.Situacao);
                PintarSituacao(cad, p);
                coluna.Controls.Add(cab);

                foreach (Secao s in cad.Secoes(p))
                {
                    if (s.Campos.Count == 0) continue;
                    coluna.Controls.Add(MontarSecao(cad, p, s));
                }
            }

            host.Controls.Add(coluna);
            host.ResumeLayout(true);
            if (rolagem > 0) host.AutoScrollPosition = new Point(0, rolagem);

            // remontou por causa de um campo: devolve o foco a ele, com o cursor no fim
            Control alvo;
            if (focar != null && cad.Controles.TryGetValue(focar, out alvo))
            {
                alvo.Focus();
                var cb = alvo as ComboBox;
                if (cb != null && cb.DropDownStyle == ComboBoxStyle.DropDown) cb.SelectionStart = cb.Text.Length;
                var tb = alvo as TextBox;
                if (tb != null) tb.SelectionStart = tb.Text.Length;
            }
        }

        // ===================================================================
        //  Memorias de defeito pre-preenchidas (nome padrao, que acompanha o nome da partida)
        // ===================================================================
        private static string AutoDef(Partida p) { return p.Sub + "_" + p.Eq + "_" + p.Fn + "_LEI_DEF"; }
        private static string AutoPrt(Partida p) { return p.Sub + "_" + p.Eq + "_" + p.Fn + "_LEI_PRT"; }

        private static bool NomeCompleto(Partida p)
        {
            return !Vazio(p.Sub) && !Vazio(p.Eq) && !Vazio(p.Fn);
        }

        /// <summary>Preenche os nomes padrao que estiverem vazios (espelho de defeito; memoria da protecao).</summary>
        private static string LimparNomeTag(string valor)
        {
            string s = (valor ?? "").Trim().ToUpperInvariant().Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
            }
            return sb.ToString().Trim('_');
        }

        private static string MontarPadraoIo(string modelo, Partida p)
        {
            string desc = LimparNomeTag(p.Descricao);
            string valor = (modelo ?? "").Replace("{DESC}", desc)
                .Replace("{SUB}", LimparNomeTag(p.Sub)).Replace("{EQ}", LimparNomeTag(p.Eq))
                .Replace("{FN}", LimparNomeTag(p.Fn));
            return LimparNomeTag(valor);
        }

        /// <summary>Tira palavras repetidas em sequencia (MEDIDOR_MEDIDOR_VAZAO vira MEDIDOR_VAZAO).</summary>
        private static string ColapsarRepetidas(string nome)
        {
            var saida = new List<string>();
            foreach (string t in (nome ?? "").Split('_'))
                if (t.Length > 0 && (saida.Count == 0 || saida[saida.Count - 1] != t)) saida.Add(t);
            return string.Join("_", saida.ToArray());
        }

        /// <param name="legado">true = nomes como eram antes (abreviados): serve para reconhecer tags automaticas antigas.</param>
        private static Dictionary<string, string> NomesAutomaticosIo(Partida p, bool legado = false)
        {
            var nomes = new Dictionary<string, string>();
            if (p == null || !NomeCompleto(p)) return nomes;
            Gerador.TipoPartida t = Gerador.BuscarTipo(p.Tipo);
            if (t == null) return nomes;
            Func<string, string, string> add = (prop, modelo) => MontarPadraoIo(modelo, p);

            if (Gerador.EhSensor(p))
            {
                // sensores: nome completo (descricao), sem palavras repetidas; "legado" = o jeito antigo, so para reconhecer tags antigas
                Func<string, string, string> sen = (prop, modelo) =>
                {
                    string n = MontarPadraoIo(modelo, p);
                    return legado ? n : ColapsarRepetidas(n);
                };
                string pMov = legado ? Cfg.LegadoChaveMovimento : Cfg.PadraoChaveMovimento;
                string pMed = legado ? Cfg.LegadoMedidor : Cfg.PadraoMedidor;
                string pTot = legado ? Cfg.LegadoTotalizador : Cfg.PadraoTotalizador;
                string pChave = legado ? Cfg.LegadoChave : Cfg.PadraoChave;
                string descricaoSensor = LimparNomeTag(p.Descricao);
                bool sensoresPasFlotador = t.SenRaspador
                    || (descricaoSensor.Contains("SENSORES") && descricaoSensor.Contains("PAS")
                        && descricaoSensor.Contains("RASPADOR") && descricaoSensor.Contains("FLOTADOR"));
                if (sensoresPasFlotador)
                {
                    if (legado)
                    {
                        nomes["TagSensor"] = sen("TagSensor", pMov);
                        nomes["TagSensor2"] = sen("TagSensor2", pMov) + "_2";
                    }
                    else
                    {
                        // Padrao fixo solicitado para as duas pas do flotador.
                        nomes["TagSensor"] = "CHAVE_MOVIMENTO_LADO_DIREITO_FLOTADOR";
                        nomes["TagSensor2"] = "CHAVE_MOVIMENTO_LADO_ESQUERDO_FLOTADOR";
                    }
                    nomes["TagConf"] = add("TagConf", Cfg.PadraoConf);
                }
                else if (t.SenVazao || t.SenAna)
                {
                    if (!legado && t.SenVazao && Maquinas.EhPreparador(p) && p.Papel == Maquinas.PapelVazao)
                    {
                        string maquina = LimparNomeTag(Regex.Replace(p.Descricao ?? "", @"\s*-\s*.*$", ""));
                        nomes["TagSensor"] = ColapsarRepetidas("MEDIDOR_DE_VAZAO_" + maquina);
                    }
                    else
                    {
                        nomes["TagSensor"] = sen("TagSensor", pMed);
                    }
                    if (t.SenVazao && !Maquinas.EhPreparador(p)) nomes["TagPulso"] = sen("TagPulso", pTot);
                }
                else if (!t.SenAna && !t.SenVazao && !t.SenRaspador)
                {
                    string palavra = t.SenAlto ? "ALTO" : "BAIXO";
                    if (legado)
                    {
                        // Mantém o reconhecimento das tags abreviadas de projetos antigos.
                        string chaveAntiga = sen("TagSensor", pChave);
                        nomes["TagSensor"] = chaveAntiga + "_" + palavra;
                    }
                    else
                    {
                        // Preparador: usar a descricao-base da maquina e o papel da linha,
                        // sem anexar "NIVEL BAIXO TANQUE" ou "NIVEL BAIXO POLIMERO".
                        if (Maquinas.EhPreparador(p) &&
                            (p.Papel == Maquinas.PapelNivBaixo || p.Papel == Maquinas.PapelNivAlto || p.Papel == Maquinas.PapelNivPolimero))
                        {
                            string maquina = LimparNomeTag(Regex.Replace(p.Descricao ?? "", @"\s*-\s*.*$", ""));
                            string complemento = p.Papel == Maquinas.PapelNivPolimero
                                ? "SILO_POLIMERO_" + maquina
                                : maquina;
                            nomes["TagSensor"] = ColapsarRepetidas("CHAVE_NIVEL_" + palavra + "_" + complemento);
                        }
                        else
                        {
                            string descricao = LimparNomeTag(p.Descricao);
                            string[] partes = descricao.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
                            descricao = string.Join("_", partes.Where(x =>
                                x != "CHAVE" && x != "NIVEL" && x != "BAIXO" && x != "ALTO").ToArray());
                            nomes["TagSensor"] = ColapsarRepetidas("CHAVE_NIVEL_" + palavra + (descricao.Length > 0 ? "_" + descricao : ""));
                        }
                    }
                }
                return nomes;
            }

            if (t.Valvula)
            {
                if (t.Di2) { nomes["TagConf"] = add("TagConf", Cfg.PadraoConf); nomes["TagConfFec"] = add("TagConfFec", Cfg.PadraoConf) + "_FECHADA"; }
                nomes["TagHab"] = add("TagHab", Cfg.PadraoHab);
                if (t.Do2) nomes["TagFec"] = add("TagFec", Cfg.PadraoHab) + "_FECHAR";
            }
            else
            {
                if (!t.Drive && !t.Rede) nomes["TagConf"] = add("TagConf", Cfg.PadraoConf);
                if (t.Bypass)
                {
                    string padraoDrive = p.Tipo.IndexOf("soft", StringComparison.OrdinalIgnoreCase) >= 0 ? Cfg.PadraoHabSoft : Cfg.PadraoHabInv;
                    nomes["TagHab"] = add("TagHab", padraoDrive);
                    nomes["TagHabC1"] = add("TagHabC1", Cfg.PadraoHabBomba1);
                    nomes["TagHabC2"] = add("TagHabC2", Cfg.PadraoHabBomba2);
                }
                else if (!t.Rede) nomes["TagHab"] = add("TagHab", Cfg.PadraoHab);
                if (t.Drive) nomes["TagDefDrive"] = add("TagDefDrive", Cfg.PadraoDef);
                if (p.Exaustor)
                {
                    nomes["TagExaConf"] = add("TagExaConf", Cfg.PadraoConf) + "_EXAUSTOR";
                    nomes["TagExaHab"] = add("TagExaHab", Cfg.PadraoHab) + "_EXAUSTOR";
                }
            }
            return nomes;
        }

        /// <summary>Tags de sensores que ainda estao com o nome automatico antigo (abreviado): (linha, campo, nome antigo, nome novo).</summary>
        private List<Tuple<Partida, string, string, string>> NomesAntigosDeSensores()
        {
            var lista = new List<Tuple<Partida, string, string, string>>();
            foreach (Partida p in _sensores)
            {
                Dictionary<string, string> leg = NomesAutomaticosIo(p, true), novo = NomesAutomaticosIo(p, false);
                foreach (var par in novo)
                {
                    string velho;
                    if (!leg.TryGetValue(par.Key, out velho) || velho == par.Value) continue;
                    PropertyInfo pi = typeof(Partida).GetProperty(par.Key);
                    if (pi == null) continue;
                    string atual = Convert.ToString(pi.GetValue(p, null) ?? "");
                    if (string.Equals(atual, velho, StringComparison.OrdinalIgnoreCase))
                        lista.Add(Tuple.Create(p, par.Key, atual, par.Value));
                }
            }
            return lista;
        }

        /// <summary>Troca as tags automaticas antigas dos sensores pelo nome completo (a tag da aba Tags tambem muda; endereco e descricao ficam).</summary>
        private int AtualizarNomesDeSensores(List<Tuple<Partida, string, string, string>> trocas)
        {
            int n = 0;
            _carregando = true;
            try
            {
                foreach (var t in trocas)
                {
                    PropertyInfo pi = typeof(Partida).GetProperty(t.Item2);
                    Io io = _ios.FirstOrDefault(x => string.Equals(x.Nome, t.Item3, StringComparison.OrdinalIgnoreCase));
                    if (io != null && !_ios.Any(x => string.Equals(x.Nome, t.Item4, StringComparison.OrdinalIgnoreCase))) io.Nome = t.Item4;
                    pi.SetValue(t.Item1, t.Item4, null);
                    n++;
                }
            }
            finally
            {
                _carregando = false;
            }
            ReligarTags();
            foreach (Cadastro c in new[] { _cadPart, _cadSen, _cadPid }) MontarEditor(c, true);
            AoMudarDados();
            AtualizarMaquinas();
            return n;
        }

        /// <summary>Ao abrir um projeto com sensores no nome abreviado antigo, oferece trocar pelo nome completo.</summary>
        private void OferecerNomesCompletosDeSensores()
        {
            Action oferta = delegate
            {
                var trocas = NomesAntigosDeSensores();
                if (trocas.Count == 0) return;
                int sensores = trocas.Select(x => x.Item1).Distinct().Count();
                if (!_confirmar("Os nomes automáticos das tags dos sensores agora são completos (com a descrição do item, como nas partidas).\n\n"
                    + "Este projeto tem " + trocas.Count + " tag(s) de " + sensores + " sensor(es) com o nome abreviado antigo. Trocar agora?\n\n"
                    + "Endereços e descrições das tags são mantidos. As tags antigas que já foram geradas no TIA continuam lá; a próxima geração cria as novas.")) return;
                int n = AtualizarNomesDeSensores(trocas);
                Log("Tags dos sensores atualizadas para o nome completo: " + n + ".");
                _modificado = true;
                AtualizarTitulo();
            };
            if (IsHandleCreated && Visible) BeginInvoke(oferta);
            else
            {
                EventHandler uma = null;
                uma = delegate { Shown -= uma; BeginInvoke(oferta); };
                Shown += uma;
            }
        }

        private static void PreencherAutomaticos(Partida p)
        {
            if (p == null || !NomeCompleto(p)) return;
            Gerador.TipoPartida tp = Gerador.BuscarTipo(p.Tipo);
            if (!Gerador.EhSensor(p) && !Gerador.EhPid(p) && (tp == null || !tp.MaquinaSis))
            {
                if (Vazio(p.TagDef)) p.TagDef = AutoDef(p);
                if (!Vazio(p.TagProtecao) && Vazio(p.TagProtecaoMem)) p.TagProtecaoMem = AutoPrt(p);
            }
            foreach (var par in NomesAutomaticosIo(p))
            {
                PropertyInfo pi = typeof(Partida).GetProperty(par.Key);
                if (pi != null && Vazio(Convert.ToString(pi.GetValue(p, null)))) pi.SetValue(p, par.Value, null);
            }
        }

        private void AtualizarTagsAutomaticasAoRenomear(Cadastro cad, Partida p, string subAntigo, string eqAntigo, string fnAntigo)
        {
            var antigo = new Partida { Sub = subAntigo, Eq = eqAntigo, Fn = fnAntigo, Tipo = p.Tipo, Descricao = p.Descricao };
            Dictionary<string, string> anteriores = NomesAutomaticosIo(antigo);
            foreach (var par in NomesAutomaticosIo(p))
            {
                PropertyInfo pi = typeof(Partida).GetProperty(par.Key);
                if (pi == null) continue;
                string atual = Convert.ToString(pi.GetValue(p, null) ?? "");
                string antes;
                if (Vazio(atual) || (anteriores.TryGetValue(par.Key, out antes) && atual == antes))
                    TrocarCampo(cad, p, par.Key, par.Value);
            }
        }

        private void AtualizarTagsAutomaticasPorModelo(Cadastro cad, Partida p, Dictionary<string, string> anteriores)
        {
            foreach (var par in NomesAutomaticosIo(p))
            {
                PropertyInfo pi = typeof(Partida).GetProperty(par.Key);
                if (pi == null) continue;
                string atual = Convert.ToString(pi.GetValue(p, null) ?? "");
                string antes;
                if (Vazio(atual) || (anteriores != null && anteriores.TryGetValue(par.Key, out antes) && atual == antes))
                    TrocarCampo(cad, p, par.Key, par.Value);
            }
        }

        /// <summary>
        /// O nome da partida mudou: as memorias que estavam com o nome padrao antigo (ou vazias)
        /// passam para o nome padrao novo. Nome digitado a mao fica como esta.
        /// </summary>
        private void AcompanharNome(Cadastro cad, Partida p, string defAntigo, string prtAntigo)
        {
            if ((cad != _cadPart && !(cad == _cadMaq && CadDe(p) == _cadPart)) || !NomeCompleto(p)) return;
            if (Vazio(p.TagDef) || p.TagDef == defAntigo) TrocarCampo(cad, p, "TagDef", AutoDef(p));
            if (!Vazio(p.TagProtecao) && (Vazio(p.TagProtecaoMem) || p.TagProtecaoMem == prtAntigo))
                TrocarCampo(cad, p, "TagProtecaoMem", AutoPrt(p));
        }

        /// <summary>Muda o valor de um campo e o controle dele no editor (sem disparar a gravacao de novo).</summary>
        private void TrocarCampo(Cadastro cad, Partida p, string prop, string valor)
        {
            typeof(Partida).GetProperty(prop).SetValue(p, valor, null);
            Control ctl;
            if (!cad.Controles.TryGetValue(prop, out ctl)) return;
            bool antes = cad.Montando;
            cad.Montando = true;
            try { ctl.Text = valor; }
            finally { cad.Montando = antes; }
        }

        private int _larguraTexto = 420;   // largura util da coluna de campos do editor (quebra dos textos)

        private void PintarSituacao(Cadastro cad, Partida p)
        {
            if (cad.Situacao == null) return;
            string r = Resumo(cad, p);
            if (r.Length == 0) r = "Preencha o nome para começar";
            cad.Situacao.Text = r;
            cad.Situacao.ForeColor = r.StartsWith("⚠") ? Tema.Amarelo : r.StartsWith("✓") ? Tema.Verde : Tema.TextoFraco;
            Tema.Medir(cad.Situacao, _larguraTexto);
        }

        private Control MontarSecao(Cadastro cad, Partida p, Secao s)
        {
            var card = new CartaoTabela(1) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12) };

            Label t = Tema.TituloCartao(s.Titulo);
            t.Margin = new Padding(0, 0, 0, s.Descricao != null ? 3 : 12);
            card.Controls.Add(t);

            if (s.Descricao != null)
            {
                var d = Tema.Ajuda(s.Descricao, _larguraTexto);
                d.Margin = new Padding(0, 0, 0, 14);
                card.Controls.Add(d);
            }

            foreach (Campo c in s.Campos)
            {
                if (c.Tipo == TipoCampo.Nota)
                {
                    var n = Tema.Ajuda(c.Ajuda, _larguraTexto);
                    n.ForeColor = c.Destaque ? Tema.Amarelo : Tema.TextoSec;
                    n.Margin = new Padding(0, 0, 0, 12);
                    card.Controls.Add(n);
                    continue;
                }
                if (c.Tipo == TipoCampo.Acionamentos)
                {
                    card.Controls.Add(MontarAcionamentos(cad, p, c));
                    continue;
                }

                // rótulo em cima do campo (como no Orquestra); a caixinha de marcar leva o próprio texto
                Label rot = Tema.Secao(c.Rotulo);
                rot.Margin = new Padding(0, 0, 0, 6);
                if (c.Tipo != TipoCampo.Marcar) card.Controls.Add(rot);

                var bloco = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink,
                    ColumnCount = 1,
                    BackColor = Color.Transparent,
                    Margin = new Padding(0, 0, 0, 16)
                };
                bloco.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                if (c.Tipo == TipoCampo.Nome) MontarNome(cad, p, c, bloco, rot);
                else MontarCampo(cad, p, c, bloco, rot);
                card.Controls.Add(bloco);
            }
            return card;
        }

        private void MontarCampo(Cadastro cad, Partida p, Campo c, TableLayoutPanel bloco, Label rot)
        {
            PropertyInfo pi = typeof(Partida).GetProperty(c.Prop);
            Func<string> lerTexto = delegate { return Convert.ToString(pi.GetValue(p, null) ?? ""); };

            Label info = null;
            Action atualizarInfo = delegate
            {
                string v = lerTexto().Trim();
                bool falta = c.Obrigatorio && v.Length == 0;
                rot.Text = Tema.Espacado(c.Rotulo) + (c.Obrigatorio ? "  •" : "");
                rot.ForeColor = falta ? Tema.Amarelo : Tema.TextoSec;
                if (info == null) return;
                string txt; Color cor;
                InfoDoCampo(c, v, out txt, out cor);
                info.Text = txt;
                info.ForeColor = cor;
                info.Visible = txt.Length > 0;
                Tema.Medir(info, _larguraTexto);
            };

            Action<object> gravar = delegate (object valor)
            {
                if (cad.Montando) return;
                string antes = Convert.ToString(pi.GetValue(p, null) ?? "");
                Dictionary<string, string> nomesAntes = (c.Prop == "Descricao" || c.Prop == "Tipo") ? NomesAutomaticosIo(p) : null;
                pi.SetValue(p, valor, null);
                if (c.Prop == "Descricao" || c.Prop == "Tipo")
                    AtualizarTagsAutomaticasPorModelo(cad, p, nomesAntes);

                // proteção preenchida: a memória vem com o nome padrão
                if (c.Prop == "TagProtecao" && !Vazio(p.TagProtecao) && Vazio(p.TagProtecaoMem) && NomeCompleto(p))
                    p.TagProtecaoMem = AutoPrt(p);

                AtualizarLinha(cad, p);
                atualizarInfo();

                // campos que mostram/escondem outros: o editor se reorganiza na hora
                bool remontar = c.Prop == "Tipo" || c.Prop == "Exaustor" || c.Prop == "Auto" || c.Prop == "Alternancia" || c.Prop == "Intertravamento"
                    || c.Prop == "Intermitente" || c.Prop == "IntermitenteDes"
                    || (c.Prop == "TagProtecao" && Vazio(antes) != Vazio(Convert.ToString(valor)));
                if (remontar)
                {
                    string foco = c.Prop;
                    BeginInvoke(new Action(delegate { MontarEditor(cad, true, foco); }));
                }
            };

            Control ctl;
            switch (c.Tipo)
            {
                case TipoCampo.Texto:
                {
                    TextBox tb = Tema.CaixaTexto();
                    tb.Text = lerTexto();
                    tb.TextChanged += delegate { gravar(tb.Text); };
                    ctl = tb;
                    break;
                }
                case TipoCampo.TextoLongo:
                {
                    // uma condicao por linha
                    TextBox tb = Tema.CaixaTexto();
                    tb.Multiline = true;
                    tb.AcceptsReturn = true;
                    tb.ScrollBars = ScrollBars.Vertical;
                    tb.Height = 78;
                    tb.Font = Tema.Mono;
                    tb.Text = lerTexto().Replace("\r\n", "\n").Replace("\n", Environment.NewLine);
                    tb.TextChanged += delegate { gravar(tb.Text.Replace("\r\n", "\n")); };
                    ctl = tb;
                    break;
                }
                case TipoCampo.Marcar:
                {
                    CheckBox cb = Tema.Marcador(c.Rotulo);
                    cb.Font = Tema.Negrito;
                    cb.Checked = (bool)pi.GetValue(p, null);
                    cb.CheckedChanged += delegate { gravar(cb.Checked); };
                    ctl = cb;
                    break;
                }
                case TipoCampo.Lista:
                {
                    ComboBox cb = Tema.Lista(false);
                    List<string> itens = c.Itens();
                    foreach (string i in itens) cb.Items.Add(i.Length == 0 ? Nenhum : i);
                    string atual = lerTexto();
                    int idx = itens.IndexOf(atual);
                    if (idx < 0 && atual.Length > 0) { cb.Items.Add(atual); idx = cb.Items.Count - 1; }
                    cb.SelectedIndex = idx < 0 ? (itens.Count > 0 ? 0 : -1) : idx;
                    cb.SelectedIndexChanged += delegate
                    {
                        string v = Convert.ToString(cb.SelectedItem);
                        gravar(v == Nenhum ? "" : v);
                    };
                    ctl = cb;
                    break;
                }
                default:   // Tag e Ref: lista editavel (escolhe ou digita)
                {
                    ComboBox cb = Tema.Lista(true);
                    var itens = new List<string> { "" };
                    if (c.Itens != null) itens.AddRange(c.Itens());
                    string atual = lerTexto();
                    if (atual.Length > 0 && !itens.Contains(atual)) itens.Add(atual);
                    cb.Items.AddRange(itens.Cast<object>().ToArray());
                    cb.SelectedIndex = itens.IndexOf(atual);   // mais garantido que .Text antes de a lista existir
                    cb.TextChanged += delegate { gravar(cb.Text.Trim()); };
                    ctl = cb;
                    info = new Label { AutoSize = true, Font = Tema.Pequena, Margin = new Padding(1, 5, 0, 0), MaximumSize = new Size(_larguraTexto, 0), BackColor = Color.Transparent, UseMnemonic = false };
                    break;
                }
            }

            if (c.Prop != null) cad.Controles[c.Prop] = ctl;
            Control quadro = Tema.Emoldurar(ctl);
            quadro.Dock = DockStyle.Fill;
            quadro.Margin = new Padding(0);
            bloco.Controls.Add(quadro);
            if (info != null) bloco.Controls.Add(info);
            if (!string.IsNullOrEmpty(c.Ajuda))
            {
                var aj = Tema.Ajuda(c.Ajuda, _larguraTexto);
                aj.ForeColor = Tema.TextoFraco;
                aj.Margin = new Padding(1, 4, 0, 0);
                bloco.Controls.Add(aj);
            }
            atualizarInfo();
        }

        /// <summary>DB . grupo . nome numa linha so (como o caminho no TIA: "FL1".ALM.BBA).</summary>
        private void MontarNome(Cadastro cad, Partida p, Campo c, TableLayoutPanel bloco, Label rot)
        {
            var linha = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 5, RowCount = 2, BackColor = Color.Transparent, Margin = new Padding(0) };
            for (int i = 0; i < 5; i++)
                linha.ColumnStyles.Add(i % 2 == 0 ? new ColumnStyle(SizeType.Percent, 33.3f) : new ColumnStyle(SizeType.Absolute, 14));

            rot.Text = Tema.Espacado(c.Rotulo) + "  •";
            Action pintar = delegate
            {
                bool falta = c.Props.Any(n => Vazio(Convert.ToString(typeof(Partida).GetProperty(n).GetValue(p, null))));
                rot.ForeColor = falta ? Tema.Amarelo : Tema.TextoSec;
            };

            for (int i = 0; i < 3; i++)
            {
                PropertyInfo pi = typeof(Partida).GetProperty(c.Props[i]);
                var cap = new Label { Text = c.Legendas[i], AutoSize = true, Font = Tema.Fonte(7.25f, FontStyle.Bold), ForeColor = Tema.TextoFraco, BackColor = Color.Transparent, Margin = new Padding(1, 0, 0, 3) };
                TextBox tb = Tema.CaixaTexto();
                tb.Text = Convert.ToString(pi.GetValue(p, null) ?? "");
                tb.TextChanged += delegate
                {
                    if (cad.Montando) return;
                    string defAntigo = AutoDef(p), prtAntigo = AutoPrt(p);
                    string subAntigo = p.Sub, eqAntigo = p.Eq, fnAntigo = p.Fn;
                    pi.SetValue(p, tb.Text, null);
                    AtualizarTagsAutomaticasAoRenomear(cad, p, subAntigo, eqAntigo, fnAntigo);
                    AcompanharNome(cad, p, defAntigo, prtAntigo);
                    AtualizarLinha(cad, p);
                    pintar();
                };
                cad.Controles[c.Props[i]] = tb;
                linha.Controls.Add(cap, i * 2, 0);
                Control quadro = Tema.Emoldurar(tb);
                quadro.Dock = DockStyle.Fill;
                quadro.Margin = new Padding(0);
                linha.Controls.Add(quadro, i * 2, 1);
                if (i < 2)
                {
                    var ponto = new Label { Text = ".", AutoSize = true, Font = Tema.Fonte(12f, FontStyle.Bold), ForeColor = Tema.TextoSec, BackColor = Color.Transparent, Margin = new Padding(2, 6, 0, 0) };
                    linha.Controls.Add(ponto, i * 2 + 1, 1);
                }
            }
            bloco.Controls.Add(linha);
            if (!string.IsNullOrEmpty(c.Ajuda))
            {
                var aj = Tema.Ajuda(c.Ajuda, _larguraTexto);
                aj.ForeColor = Tema.TextoFraco;
                aj.Margin = new Padding(1, 4, 0, 0);
                bloco.Controls.Add(aj);
            }
            pintar();
        }

        /// <summary>Linha de informacao abaixo de um campo de tag ou de referencia.</summary>
        private void InfoDoCampo(Campo c, string v, out string texto, out Color cor)
        {
            texto = "";
            cor = Tema.TextoSec;
            if (c.Tipo == TipoCampo.Ref)
            {
                if (v.Length == 0) return;
                texto = c.Info != null ? c.Info(v) : "";
                cor = texto.StartsWith("⚠") ? Tema.Amarelo : Tema.Verde;
                return;
            }
            if (c.Tipo != TipoCampo.Tag) return;
            if (v.Length == 0)
            {
                if (c.Obrigatorio) { texto = "Escolha da lista (página Tags de PLC) ou digite o nome de uma tag que já exista no PLC."; cor = Tema.TextoFraco; }
                return;
            }
            Io io = _ios.FirstOrDefault(x => x.Nome != null && x.Nome.Trim() == v);
            if (io == null)
            {
                texto = "⚠ Não está na página Tags de PLC — será usada se já existir no PLC.";
                cor = Tema.Amarelo;
            }
            else
            {
                texto = "✓ " + (Vazio(io.Endereco) ? "sem endereço" : io.Endereco) + "   ·   " + io.Tipo
                    + (Vazio(io.Descricao) ? "" : "   ·   " + io.Descricao);
                cor = Vazio(io.Endereco) ? Tema.Amarelo : Tema.Verde;
            }
        }

        /// <summary>Lista de caixinhas: acionamentos que o PID / medidor comanda.</summary>
        private Control MontarAcionamentos(Cadastro cad, Partida dono, Campo c)
        {
            var fluxo = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 0, 0, 6)
            };
            var rot = new Label { Text = c.Rotulo, AutoSize = true, Font = Tema.Negrito, ForeColor = Tema.Texto, Margin = new Padding(0, 0, 0, 4) };
            fluxo.Controls.Add(rot);
            if (!string.IsNullOrEmpty(c.Ajuda))
            {
                var aj = Tema.Ajuda(c.Ajuda, 600);
                aj.ForeColor = Tema.TextoFraco;
                aj.Margin = new Padding(0, 0, 0, 8);
                fluxo.Controls.Add(aj);
            }

            List<Partida> cands = c.Candidatos();
            if (cands.Count == 0)
            {
                var nada = Tema.Ajuda("Nenhum acionamento compatível cadastrado ainda (página Partidas).", 600);
                nada.ForeColor = Tema.Amarelo;
                fluxo.Controls.Add(nada);
                return fluxo;
            }
            foreach (Partida a in cands)
            {
                Partida alvo = a;
                CheckBox cb = Tema.Marcador(alvo.Sub + "." + alvo.Eq + "." + alvo.Fn
                    + (Vazio(alvo.Descricao) ? "" : "   —   " + alvo.Descricao) + "   (" + alvo.Tipo + ")");
                cb.Checked = c.Ligado(alvo);
                cb.Margin = new Padding(0, 2, 0, 2);
                cb.CheckedChanged += delegate
                {
                    c.Ligar(alvo, cb.Checked);
                    AtualizarLinha(_cadPart, alvo);
                    AtualizarLinha(cad, dono);
                };
                fluxo.Controls.Add(cb);
            }
            return fluxo;
        }

        /// <summary>Atualiza a linha do item na lista (resumo, colunas) sem remontar o editor.</summary>
        private void AtualizarLinha(Cadastro cad, Partida p)
        {
            int i = cad.Lista.IndexOf(p);
            if (i < 0) return;
            bool antes = cad.Montando;
            cad.Montando = true;
            try { cad.Lista.ResetItem(i); }
            finally { cad.Montando = antes; }
            if (p == cad.Editando) PintarSituacao(cad, p);
        }

        // ===================================================================
        //  Campos de cada tipo (o que antes eram dezenas de colunas)
        // ===================================================================
        private static Campo Txt(string prop, string rotulo, string ajuda)
        {
            return new Campo { Prop = prop, Rotulo = rotulo, Ajuda = ajuda, Tipo = TipoCampo.Texto };
        }

        private static Campo CampoTag(string prop, string rotulo, string ajuda, Func<List<string>> itens, bool obrigatorio = true)
        {
            return new Campo { Prop = prop, Rotulo = rotulo, Ajuda = ajuda, Tipo = TipoCampo.Tag, Itens = itens, Obrigatorio = obrigatorio };
        }

        private static Campo Nome(string rotulo, string[] legendas, string ajuda)
        {
            return new Campo { Tipo = TipoCampo.Nome, Rotulo = rotulo, Props = new[] { "Sub", "Eq", "Fn" }, Legendas = legendas, Ajuda = ajuda };
        }

        private static Campo Nota(string texto, bool destaque = false)
        {
            return new Campo { Tipo = TipoCampo.Nota, Ajuda = texto, Destaque = destaque };
        }

        private List<Secao> SecoesPartida(Partida p)
        {
            Gerador.TipoPartida tm = Gerador.BuscarTipo(p.Tipo);
            if (tm != null && (tm.MaquinaSis || tm.MaquinaAgua)) return SecoesNucleoMaquina(p, tm);
            List<Secao> normais = SecoesPartidaNormal(p);
            if (Maquinas.EhParte(p))
            {
                normais.RemoveAll(x => x.Titulo == "Lógica");
                var nota = new Secao("Máquina " + p.Maquina, null);
                nota.Campos.Add(Nota("Faz parte da máquina " + p.Maquina + ": o automático e o intertravamento vêm do bloco da máquina "
                    + "(CMD_INT = automático E não habilitado pelo bloco). Aqui só as entradas, saídas e opções do motor."));
                normais.Insert(1, nota);
            }
            return normais;
        }

        /// <summary>Nucleo da maquina (SIS.AUT) e valvula de agua do preparador.</summary>
        private List<Secao> SecoesNucleoMaquina(Partida p, Gerador.TipoPartida t)
        {
            var lista = new List<Secao>();
            var id = new Secao("Identificação", null);
            id.Campos.Add(Nome("Nome no DB", new[] { "DB", "GRUPO", "NOME" }, "Faz parte da máquina " + p.Maquina + "."));
            id.Campos.Add(Txt("Descricao", "Descrição", "Título da rede."));
            lista.Add(id);

            var maq = new Secao("Máquina " + p.Maquina, null);
            if (t.MaquinaSis)
            {
                var todas = _partidas.Concat(_sensores).Where(x => x.Maquina == p.Maquina && x != p).ToList();
                maq.Campos.Add(Nota("Gera o bloco da máquina (BL_POLIMERO_PREPARO_FLOCULANTE) com os sensores e set-points do SIS.AUT, "
                    + "a visualização e o automático (liga/desliga as partes pelo intertravamento, defeito geral, desliga temporizado)."));
                maq.Campos.Add(Nota("Itens: " + string.Join(", ", todas.Select(x => x.Sub + "." + x.Eq + "." + x.Fn).ToArray()) + "."));
                maq.Campos.Add(Nota("Usa o primeiro ciclo \"" + Cfg.TagPrimeiroCiclo + "\" e o temporizador " + p.Sub + ".SIS."
                    + "DES (IEC_TIMER no DB). Para remover a máquina inteira, use a página Máquinas."));
            }
            else
            {
                maq.Campos.Add(CampoTag("TagHab", "Saída da válvula (DO)", "Bobina de LEI_ABR.", Saidas));
                maq.Campos.Add(CampoTag("TagDef", "Espelho de defeito", "Memória ligada ao LEI_DEF.", Saidas, false));
                maq.Campos.Add(Nota("Válvula Alimentação Preparador em BL_PARTIDA_HIBRIDA: confirma pelo fluxo do medidor (ou aberta em manual), "
                    + "tempo de falha = SIS.AUT.SET_FLU, intertravamento pelo bloco da máquina."));
            }
            lista.Add(maq);
            return lista;
        }

        private List<Secao> SecoesPartidaNormal(Partida p)
        {
            Gerador.TipoPartida t = Gerador.BuscarTipo(p.Tipo);
            var lista = new List<Secao>();

            var id = new Secao("Identificação", null);
            id.Campos.Add(Nome("Nome no DB", new[] { "SUBSISTEMA", "EQUIPAMENTO", "FUNÇÃO" },
                "Como fica no TIA: \"FL1\".ALM.BBA. O subsistema vira o DB " + Cfg.Prefixo + "FL1 e a FC " + Cfg.Prefixo + "PARTIDAS_FL1."));
            id.Campos.Add(new Campo
            {
                Prop = "Tipo", Rotulo = "Tipo de partida", Tipo = TipoCampo.Lista,
                Itens = delegate { return Gerador.NomesTiposPartida.ToList(); },
                Ajuda = "Define o bloco usado (BL_PARTIDA_HIBRIDA, BL_INVERSOR_6 ou BL_VALVULA) e os campos abaixo."
            });
            id.Campos.Add(Txt("Descricao", "Descrição", "Vai no comentário do DB e no título da rede."));
            lista.Add(id);
            if (t == null) return lista;

            // --- entradas e saidas ---
            var io = new Secao("Entradas e saídas", "Tags de PLC ligadas no bloco. Escolha da lista (vem da página Tags de PLC) ou digite o nome.");
            if (t.Valvula && t.Analogica)
            {
                io.Campos.Add(CampoTag("TagRef", "Saída analógica de posição (AO)", "OUT VALOR do BL_ANALOGICA (0..100% → 0..27648).", SaidasAna));
                if (t.Ai) io.Campos.Add(CampoTag("TagPosAi", "Leitura de posição (AI)", "IN ENTRADA do BL_ANALOGICA que lê a posição (LEI_POS).", EntradasAna));
            }
            else if (t.Valvula)
            {
                if (t.Di2)
                {
                    io.Campos.Add(CampoTag("TagConf", "Confirmação aberta (DI)", "VAL_CNF_ABR.", Entradas));
                    io.Campos.Add(CampoTag("TagConfFec", "Confirmação fechada (DI)", "VAL_CNF_FEC.", Entradas));
                }
                io.Campos.Add(CampoTag("TagHab", "Saída abre (DO)", "Bobina de LEI_ABR.", Saidas));
                if (t.Do2) io.Campos.Add(CampoTag("TagFec", "Saída fecha (DO)", "Bobina de LEI_FEC.", Saidas));
            }
            else
            {
                if (!t.Drive && !t.Rede)
                    io.Campos.Add(CampoTag("TagConf", "Confirmação do contator (DI)", "Retorno do contator → MOT_LEI_CN1.", Entradas));
                if (!t.Rede)
                    io.Campos.Add(CampoTag("TagHab", t.Bypass ? "Habilita do drive (DO)" : "Habilita (DO)",
                        "Saída que liga o " + (t.Drive ? "inversor/soft" : "contator") + " (bobina de LEI_HAB).", Saidas));
                if (t.Bypass)
                {
                    io.Campos.Add(CampoTag("TagHabC1", "Habilita contator 1 (DO)", "Motor titular → " + (t.Rede ? "MOT_LEI_HA1" : "MOT_CMD_HA1") + ".", Saidas));
                    io.Campos.Add(CampoTag("TagHabC2", "Habilita contator 2 (DO)", "Motor reserva → " + (t.Rede ? "MOT_LEI_HA2" : "MOT_CMD_HA2") + ".", Saidas));
                }
                if (t.Drive) io.Campos.Add(CampoTag("TagDefDrive", "Defeito do drive (DI)", "Entra negado em MOT_LEI_TRP.", Entradas));
                if (t.Velocidade) io.Campos.Add(CampoTag("TagRef", "Referência de velocidade (AO)", "Saída analógica ligada em MOT_LEI_VEL.", SaidasAna));
                if (t.Rede) io.Campos.Add(Nota("Inversor por rede: não usa IOs. O pino INV_SET_END (hardware do drive) fica aberto para escolher no TIA."));
            }
            io.Campos.Add(CampoTag("TagDef", "Espelho de defeito", "Memória ligada ao LEI_DEF. Já vem com o nome padrão ("
                + AutoDef(p) + ") e acompanha o nome da partida; troque se o projeto usar outro.", Saidas, false));
            lista.Add(io);

            // --- logica ---
            var log = new Secao("Lógica", null);
            log.Campos.Add(new Campo { Prop = "Auto", Rotulo = "Operação automática", Tipo = TipoCampo.Marcar,
                Ajuda = "Liga/desliga pelo modo automático (Set/Reset do " + (t.Valvula ? "CMD_ABR" : "CMD_LIG") + "). Defeito tira do automático." });
            log.Campos.Add(new Campo { Prop = "Intertravamento", Rotulo = "Intertravamento", Tipo = TipoCampo.Marcar,
                Ajuda = "Cria a rede do CMD_INT: (condições) E automático E NÃO bypass. Sem condições, fica um contato vazio para preencher no TIA. Os contatos do CMD_INT na lógica do automático saem sempre, mesmo sem marcar." });
            if (p.Intertravamento)
                log.Campos.Add(new Campo { Prop = "CondicoesInt", Rotulo = "Condições do intertravamento", Tipo = TipoCampo.TextoLongo,
                    Ajuda = "Uma por linha, em OU (qualquer linha ativa bloqueia a partida). NÃO na frente nega; E junta termos na mesma linha. "
                        + "Ex.: SEN.EQU.TAN.LEI_BAI  ·  NÃO FL1.ALM.BB1.LEI_HAB E NÃO FL1.ALM.BB2.LEI_HAB  ·  ou uma tag do PLC." });
            log.Campos.Add(new Campo { Prop = "Alternancia", Rotulo = "Alternância", Tipo = TipoCampo.Marcar,
                Ajuda = "Alterna com a outra do par: as marcadas do mesmo equipamento formam pares na ordem da lista (1ª↔2ª, 3ª↔4ª). Exige automático." });
            log.Campos.Add(new Campo { Prop = "Intermitente", Rotulo = "Intermitente: tempo ligado", Tipo = TipoCampo.Lista,
                Itens = delegate { return Gerador.UnidadesIntermitente.ToList(); },
                Ajuda = "BL_INTERMITENTE: liga e desliga em ciclos. Escolha a unidade do tempo ligado. Exige automático." });
            if (!Vazio(p.Intermitente) || !Vazio(p.IntermitenteDes))
                log.Campos.Add(new Campo { Prop = "IntermitenteDes", Rotulo = "Intermitente: tempo desligado", Tipo = TipoCampo.Lista,
                    Itens = delegate { return Gerador.UnidadesIntermitente.ToList(); },
                    Ajuda = Nenhum + " = mesma unidade do tempo ligado." });
            if ((p.Alternancia || !Vazio(p.Intermitente)) && !p.Auto)
                log.Campos.Add(Nota("⚠ Alternância e intermitente só funcionam com operação automática marcada.", true));
            lista.Add(log);

            // --- protecao e exaustor ---
            var prot = new Secao("Proteção e exaustor",
                "Sinais que geram defeito e desligam o equipamento: temperatura alta, monitor de condições, tampa de proteção... "
                + "e, nos sopradores, o exaustor da cabine.");
            prot.Campos.Add(new Campo { Prop = "DefeitosExtras", Rotulo = "Defeitos externos", Tipo = TipoCampo.TextoLongo,
                Ajuda = "Um por linha (OU): desligam a partida (resetam " + cmdLigDe(t) + " e CMD_AUT) e entram na severidade 1. "
                    + "Ex.: SEN.FL1.RAS.LEI_DED e SEN.FL1.RAS.LEI_DEE (pás do raspador). NÃO na frente nega." });
            prot.Campos.Add(CampoTag("TagProtecao", "Proteção do equipamento (DI)",
                "Contato NF: quando abre, seta a memória de defeito, que desliga a partida (reseta " + cmdLigDe(t) + " e CMD_AUT) "
                + "e entra na severidade 1 (MOT_LEI_SV1 do BL_SEVERIDADE). "
                + "O reset geral (" + (Vazio(Cfg.TagReset) ? "configuração" : Cfg.TagReset) + ") limpa a memória. Vazio = sem proteção.",
                Entradas, false));
            if (!Vazio(p.TagProtecao))
                prot.Campos.Add(CampoTag("TagProtecaoMem", "Memória de defeito da proteção",
                    "Já vem com o nome padrão (" + AutoPrt(p) + ") e acompanha o nome da partida; troque se o projeto usar outro, "
                    + "ex.: MBB_SA1_TPA_LEI_DEF (temperatura alta) ou MBB_ANX_AG1_LEI_MON (monitor). Declare com endereço na página Tags.",
                    Saidas, false));
            if (!t.Valvula)
            {
                prot.Campos.Add(new Campo
                {
                    Prop = "Exaustor", Rotulo = "Exaustor da cabine", Tipo = TipoCampo.Marcar,
                    Ajuda = "Sopradores de ar: cria o struct EXA e uma rede própria logo depois desta, com BL_PARTIDA_HIBRIDA e BL_SEVERIDADE. "
                        + "O exaustor liga junto com o soprador e o defeito dele desliga o soprador."
                });
                if (p.Exaustor)
                {
                    prot.Campos.Add(CampoTag("TagExaConf", "Confirmação do exaustor (DI)", "MOT_LEI_CN1 do bloco do exaustor.", Entradas));
                    prot.Campos.Add(CampoTag("TagExaHab", "Habilita do exaustor (DO)", "Bobina de EXA.LEI_HAB.", Saidas));
                    prot.Campos.Add(Nota("Espelho de defeito do exaustor: " + Gerador.TagDefExa(_partidas, p) + " (declare com endereço na página Tags)."));
                }
            }
            lista.Add(prot);

            // --- modulacao ---
            var mod = new Secao("Modulação", "Velocidade ou dosagem comandada por outro elemento. Também dá para marcar isso no próprio PID ou medidor.");
            if (!t.Valvula)
                mod.Campos.Add(new Campo
                {
                    Prop = "ModVazao", Rotulo = "Modula pela vazão", Tipo = TipoCampo.Ref, Itens = MedidoresModulacao,
                    Ajuda = "Medidor com modulação de químicos (página Sensores). Liga SIS_CMD_AUT = CMD_AUT e SIS_LEI_VAZ = LEI_VQP.",
                    Info = delegate (string v)
                    {
                        return MedidoresModulacao().Contains(v)
                            ? "✓ SIS_LEI_VAZ = " + Cfg.Prefixo + v + ".LEI_VQP"
                            : "⚠ Não é um medidor com modulação na página Sensores (será usado se já existir no projeto).";
                    }
                });
            if (t.Velocidade || t.Rede)
                mod.Campos.Add(new Campo
                {
                    Prop = "ModPid", Rotulo = "Modula pelo PID", Tipo = TipoCampo.Ref, Itens = PidsCadastrados,
                    Ajuda = "PID da página PIDs que comanda a velocidade. Vários acionamentos podem usar o mesmo PID.",
                    Info = delegate (string v)
                    {
                        return PidsCadastrados().Contains(v)
                            ? "✓ Velocidade = " + Cfg.Prefixo + v + ".LEI_VEL (0..100%) escalada para 0..SET_VEL"
                            : "⚠ Não está na página PIDs (será usado se já existir no projeto).";
                    }
                });
            else if (!t.Valvula)
                mod.Campos.Add(Nota("Este tipo não tem velocidade, então não pode ser modulado por PID (só inversor, diafragma e inversor por rede)."));
            lista.Add(mod);
            return lista;
        }

        private static string cmdLigDe(Gerador.TipoPartida t)
        {
            return t != null && t.Valvula ? "CMD_ABR" : "CMD_LIG";
        }

        private List<Secao> SecoesSensor(Partida p)
        {
            Gerador.TipoPartida t = Gerador.BuscarTipo(p.Tipo);
            var lista = new List<Secao>();

            var id = new Secao("Identificação", null);
            id.Campos.Add(Nome("Nome no DB", new[] { "DB", "GRUPO", "SENSOR" },
                "Como fica no TIA: \"SEN\".COA.TBA. O DB dos sensores normalmente é SEN."));
            id.Campos.Add(new Campo
            {
                Prop = "Tipo", Rotulo = "Tipo de sensor", Tipo = TipoCampo.Lista,
                Itens = delegate { return Gerador.NomesTiposSensor.ToList(); },
                Ajuda = "Define o bloco usado e os campos abaixo."
            });
            id.Campos.Add(Txt("Descricao", "Descrição", "Vai no comentário do DB e no título da rede."));
            lista.Add(id);
            if (t == null) return lista;

            var io = new Secao("Ligações", null);
            if (t.SenRaspador)
            {
                io.Campos.Add(CampoTag("TagSensor", "Sensor pá lado direito (DI)", "SEN_LEI_DIR do BL_SENSOR_RASPADOR.", Entradas));
                io.Campos.Add(CampoTag("TagSensor2", "Sensor pá lado esquerdo (DI)", "SEN_LEI_LES.", Entradas));
                io.Campos.Add(CampoTag("TagConf", "Confirmação da partida do raspador (DI)", "MOT_LEI_CNF.", Entradas));
                io.Campos.Add(Nota("Gera as memórias de defeito " + p.TagDefDireito + " e " + p.TagDefEsquerdo + " (declare com endereço na página Tags)."));
            }
            else if (t.SenVazao)
            {
                io.Campos.Add(CampoTag("TagSensor", "Vazão instantânea (AI)", "IN ENTRADA do BL_ANALOGICA → LEI_VAZ.", EntradasAna));
                if (!Maquinas.EhPreparador(p))
                    io.Campos.Add(CampoTag("TagPulso", "Pulso do totalizador (DI)", "SEN_LEI_PUL do BL_SENSOR_MEDIDOR_VAZAO.", Entradas));
                else
                    io.Campos.Add(Nota("Máquina " + p.Maquina + ": o totalizador conta pelo tempo com a água habilitada (SIS.AUT.LEI_EAG E "
                        + Cfg.TagClock1Hz + "), com a vazão instantânea como fator; o fim da batelada (LEI_FIM) zera. Só roda em automático."));
            }
            else if (t.SenAna)
            {
                io.Campos.Add(CampoTag("TagSensor", "Entrada analógica (AI)", "IN ENTRADA do BL_ANALOGICA (escala SET_MIN..SET_MAX → SET_OMI..SET_UNM).", EntradasAna));
            }
            else
            {
                io.Campos.Add(CampoTag("TagSensor", "Chave de nível (DI)", t.SenAlto ? "SEN_SET_ALT do BL_SENSOR_NIVEL_DIG." : "SEN_SET_BAI do BL_SENSOR_NIVEL_DIG.", Entradas));
                io.Campos.Add(Nota("Nível baixo e alto digitais do mesmo grupo viram um único BL_SENSOR_NIVEL_DIG (1º baixo com 1º alto, na ordem da lista)."));
            }
            lista.Add(io);

            if (t.SenVazaoMod)
            {
                string refMed = Ref(p);
                var mod = new Secao("Bombas moduladas por esta vazão", null);
                mod.Campos.Add(new Campo
                {
                    Tipo = TipoCampo.Acionamentos,
                    Rotulo = "Marque as bombas que dosam conforme esta vazão",
                    Ajuda = "Nelas: SIS_CMD_AUT = CMD_AUT e SIS_LEI_VAZ = " + Cfg.Prefixo + refMed + ".LEI_VQP.",
                    Candidatos = delegate
                    {
                        return _partidas.Where(x => { Gerador.TipoPartida tx = Gerador.BuscarTipo(x.Tipo); return tx != null && !tx.Valvula; }).ToList();
                    },
                    Ligado = delegate (Partida x) { return string.Equals((x.ModVazao ?? "").Trim(), refMed, StringComparison.OrdinalIgnoreCase); },
                    Ligar = delegate (Partida x, bool sim)
                    {
                        if (sim) x.ModVazao = refMed;
                        else if (string.Equals((x.ModVazao ?? "").Trim(), refMed, StringComparison.OrdinalIgnoreCase)) x.ModVazao = "";
                    }
                });
                lista.Add(mod);
            }
            return lista;
        }

        private List<Secao> SecoesPid(Partida p)
        {
            var lista = new List<Secao>();
            string refPid = Ref(p);

            var id = new Secao("Identificação", null);
            id.Campos.Add(Nome("Nome no DB", new[] { "DB", "GRUPO", "NOME" },
                "Como fica no TIA: \"BIO\".SA1.PID. Normalmente no grupo do 1º acionamento, com o nome PID."));
            id.Campos.Add(Txt("Descricao", "Descrição", "Título da rede. Ex.: PID CORREÇÃO OD REATOR AERADO."));
            lista.Add(id);

            var leitura = new Secao("Leitura", null);
            leitura.Campos.Add(new Campo
            {
                Prop = "TagSensor", Rotulo = "Sensor (Input do PID)", Tipo = TipoCampo.Ref, Itens = SensoresAnalogicos, Obrigatorio = true,
                Ajuda = "Nível analógico ou analítico da página Sensores. Input = LEI_NIV; limites do PID = SET_OMI / SET_UNM; CMD_LOP coloca o PID em manual.",
                Info = delegate (string v)
                {
                    return SensoresAnalogicos().Contains(v)
                        ? "✓ Input = " + Cfg.Prefixo + v + ".LEI_NIV"
                        : "⚠ Não é um sensor analógico da página Sensores (será usado se já existir no projeto).";
                }
            });
            lista.Add(leitura);

            var acao = new Secao("Ação do controle", null);
            acao.Campos.Add(new Campo
            {
                Prop = "PidInverso", Rotulo = "Inverter ação do PID", Tipo = TipoCampo.Marcar,
                Ajuda = "PID_Compact: Config.InvertControl. Marque quando aumentar a saída faz a variável cair (ex.: bomba de ácido baixando o pH). "
                    + "Valem na criação do PID; se o PID já existe no TIA, marcar aplica a inversão nele (desmarcar não desfaz)."
            });
            lista.Add(acao);

            var acion = new Secao("Acionamentos modulados", null);
            acion.Campos.Add(new Campo
            {
                Tipo = TipoCampo.Acionamentos,
                Rotulo = "Marque os acionamentos que este PID comanda",
                Ajuda = "Só aparecem os que têm velocidade (inversor, diafragma e inversor por rede). Neles o MOT_SET_VEL passa a vir de "
                    + Cfg.Prefixo + refPid + ".LEI_VEL (0..100%) escalado para 0..SET_VEL.",
                Candidatos = delegate
                {
                    return _partidas.Where(x => { Gerador.TipoPartida tx = Gerador.BuscarTipo(x.Tipo); return tx != null && (tx.Velocidade || tx.Rede); }).ToList();
                },
                Ligado = delegate (Partida x) { return string.Equals((x.ModPid ?? "").Trim(), refPid, StringComparison.OrdinalIgnoreCase); },
                Ligar = delegate (Partida x, bool sim)
                {
                    if (sim) x.ModPid = refPid;
                    else if (string.Equals((x.ModPid ?? "").Trim(), refPid, StringComparison.OrdinalIgnoreCase)) x.ModPid = "";
                }
            });
            acion.Campos.Add(Nota(Cfg.PidNoOb
                ? "O PID roda direto numa rede do OB" + Cfg.NumeroObPid + " (Cyclic interrupt)."
                : "O PID roda na FC " + Gerador.NomeFcPidDe(Vazio(p.Sub) ? "<DB>" : p.Sub.Trim())
                    + ": chame-a no OB de interrupção cíclica (ex.: Cyclic interrupt [OB30])."));
            lista.Add(acion);
            return lista;
        }

        private static string Ref(Partida p)
        {
            return (p.Sub ?? "").Trim() + "." + (p.Eq ?? "").Trim() + "." + (p.Fn ?? "").Trim();
        }

        private bool _noResumo2;
        private string Resumo2(Cadastro cad, Partida p)
        {
            _noResumo2 = true;
            try { return Resumo(cad, p); }
            finally { _noResumo2 = false; }
        }

        /// <summary>Texto da coluna RESUMO: o que falta (amarelo) ou as opcoes ligadas.</summary>
        private string Resumo(Cadastro cad, Partida p)
        {
            bool naMaquina = cad != null && cad == _cadMaq;   // dentro da pagina da maquina: sem o prefixo "Maquina X"
            if (naMaquina) cad = CadDe(p);
            if (PartidaVazia(p)) return "";
            var faltam = new List<string>();
            foreach (Secao s in cad.Secoes(p))
            {
                foreach (Campo c in s.Campos)
                {
                    if (!c.Obrigatorio || c.Prop == null) continue;
                    object v = typeof(Partida).GetProperty(c.Prop).GetValue(p, null);
                    if (Vazio(Convert.ToString(v))) faltam.Add(c.Rotulo.Replace(" (DI)", "").Replace(" (DO)", "").Replace(" (AI)", "").Replace(" (AO)", ""));
                }
            }
            if (Vazio(p.Sub) || Vazio(p.Eq) || Vazio(p.Fn)) faltam.Insert(0, "nome");
            if (Gerador.BuscarTipo(p.Tipo) == null) faltam.Insert(0, "tipo");

            if (!_noResumo2 && !naMaquina && !string.IsNullOrEmpty(p.Maquina) && faltam.Count == 0)
            {
                string r0 = Resumo2(cad, p);
                return r0.StartsWith("⚠") ? r0 : "✓ Máquina " + p.Maquina + (r0.Length > 2 ? " · " + r0.Substring(2) : "");
            }
            if (cad == _cadPid)
            {
                string r = Ref(p);
                int n = _partidas.Count(x => string.Equals((x.ModPid ?? "").Trim(), r, StringComparison.OrdinalIgnoreCase));
                if (faltam.Count > 0) return "⚠ Falta: " + string.Join(", ", faltam.ToArray());
                return n == 0 ? "⚠ Não comanda nenhum acionamento" : "✓ Comanda " + n + " acionamento(s)";
            }
            if (faltam.Count > 0) return "⚠ Falta: " + string.Join(", ", faltam.ToArray());

            var op = new List<string>();
            if (p.Auto) op.Add("Automático");
            if (p.Intertravamento) op.Add("Intertrav.");
            if (p.Alternancia) op.Add("Alternância");
            if (!Vazio(p.Intermitente) || !Vazio(p.IntermitenteDes)) op.Add("Intermitente");
            if (!Vazio(p.ModVazao)) op.Add("Vazão " + p.ModVazao);
            if (!Vazio(p.ModPid)) op.Add("PID " + p.ModPid);
            if (!Vazio(p.TagProtecao)) op.Add("Proteção");
            if (p.Exaustor) op.Add("Exaustor");
            if (cad == _cadSen)
            {
                Gerador.TipoPartida t = Gerador.BuscarTipo(p.Tipo);
                if (t != null && t.SenVazaoMod)
                {
                    string r = Ref(p);
                    int n = _partidas.Count(x => string.Equals((x.ModVazao ?? "").Trim(), r, StringComparison.OrdinalIgnoreCase));
                    op.Add(n + " bomba(s) modulada(s)");
                }
            }
            return "✓ " + (op.Count > 0 ? string.Join(" · ", op.ToArray()) : "OK");
        }

        // --- listas para os campos --------------------------------------------------
        private static bool EhBool(Io i) { return string.Equals(i.Tipo, "Bool", StringComparison.OrdinalIgnoreCase); }

        private static bool EhNumerico(Io i)
        {
            return new[] { "Int", "UInt", "Word", "Real", "DInt" }.Any(x => string.Equals(i.Tipo, x, StringComparison.OrdinalIgnoreCase));
        }

        private List<string> TagsDe(Func<Io, bool> filtro)
        {
            return _ios.Where(i => !Vazio(i.Nome) && filtro(i)).Select(i => i.Nome.Trim()).Distinct().ToList();
        }

        private List<string> Entradas() { return TagsDe(i => EhBool(i) && (i.Area == "DI" || i.Area == "MEM" || i.Area == "OUTRO")); }
        private List<string> Saidas() { return TagsDe(i => EhBool(i) && (i.Area == "DO" || i.Area == "MEM" || i.Area == "OUTRO")); }
        private List<string> EntradasAna() { return TagsDe(i => EhNumerico(i) && (i.Area == "AI" || i.Area == "MEM" || i.Area == "OUTRO")); }
        private List<string> SaidasAna() { return TagsDe(i => EhNumerico(i) && (i.Area == "AO" || i.Area == "MEM" || i.Area == "OUTRO")); }

        private List<string> MedidoresModulacao()
        {
            return _sensores
                .Where(s => { Gerador.TipoPartida t = Gerador.BuscarTipo(s.Tipo); return t != null && t.SenVazaoMod; })
                .Where(s => !Vazio(s.Sub) && !Vazio(s.Eq) && !Vazio(s.Fn))
                .Select(Ref).Distinct().ToList();
        }

        private List<string> SensoresAnalogicos()
        {
            return _sensores
                .Where(s => { Gerador.TipoPartida t = Gerador.BuscarTipo(s.Tipo); return t != null && t.SenAna; })
                .Where(s => !Vazio(s.Sub) && !Vazio(s.Eq) && !Vazio(s.Fn))
                .Select(Ref).Distinct().ToList();
        }

        private List<string> PidsCadastrados()
        {
            return _pids.Where(s => !Vazio(s.Sub) && !Vazio(s.Eq) && !Vazio(s.Fn)).Select(Ref).Distinct().ToList();
        }

        /// <summary>Algo mudou nas listas: contadores, "Usada em" e resumos.</summary>
        private void AoMudarDados()
        {
            MarcarModificado();
            AtualizarContadores();
            if (_paginaAtual == _pagTags) AtualizarResumoTags();
            if (_paginaAtual == _pagModbus) AtualizarModbus();
            if (_paginaAtual == _pagMaquinas) AtualizarMaquinas();
            AtualizarFiltrosMaquina();
            if (_gridIos != null) _gridIos.Invalidate();
            foreach (Cadastro c in new[] { _cadPart, _cadSen, _cadPid })
                if (c != null && c.Grade != null) c.Grade.Invalidate();
        }

        // ===================================================================
        //  Tags de PLC
        // ===================================================================
        private Control CriarPaginaTags()
        {
            var cartao = new Cartao { Padding = new Padding(16, 14, 16, 12) };

            _gridIos = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersWidth = 26,
                SelectionMode = DataGridViewSelectionMode.RowHeaderSelect
            };
            Tema.Grade(_gridIos);
            _gridIos.DataError += delegate (object s, DataGridViewDataErrorEventArgs e) { e.ThrowException = false; };
            _gridIos.Columns.Add(Col("Nome", "TAG", 270));
            _gridIos.Columns.Add(ColLista("Area", "ÁREA", Io.Areas, 60));
            _gridIos.Columns.Add(Col("Numero", "NÚMERO", 70));
            var colEnd = Col("Endereco", "ENDEREÇO", 80);
            colEnd.ReadOnly = true;
            colEnd.DefaultCellStyle.ForeColor = Tema.TextoSec;
            _gridIos.Columns.Add(colEnd);
            _gridIos.Columns.Add(ColLista("Tipo", "TIPO", TiposDados, 60));
            _gridIos.Columns.Add(Col("Descricao", "DESCRIÇÃO", 230));
            _colUsada = new DataGridViewTextBoxColumn { HeaderText = "USADA EM", ReadOnly = true, FillWeight = 220, SortMode = DataGridViewColumnSortMode.NotSortable };
            _colUsada.DefaultCellStyle.ForeColor = Tema.TextoSec;
            _gridIos.Columns.Add(_colUsada);
            _gridIos.DataSource = _ios;

            _gridIos.Columns["Area"].ToolTipText =
                "DI: entrada digital (%I x.y)   DO: saída digital (%Q x.y)\nAI: entrada analógica (%IW n)   AO: saída analógica (%QW n)\nMEM: memória (%M x.y ou %MW n)   OUTRO: endereço completo";
            _gridIos.Columns["Numero"].ToolTipText =
                "Só o número. DI, DO e MEM bit: byte.bit (ex.: 0.2). AI, AO e MEM palavra: só o endereço em bytes (ex.: 100).";

            _gridIos.KeyDown += delegate (object s, KeyEventArgs e)
            {
                if (e.Control && e.KeyCode == Keys.V && !_gridIos.IsCurrentCellInEditMode)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    ColarTags();
                }
            };
            _gridIos.CellFormatting += delegate (object s, DataGridViewCellFormattingEventArgs e)
            {
                if (e.RowIndex < 0 || e.RowIndex >= _ios.Count) return;
                e.CellStyle.BackColor = CorDaArea(_ios[e.RowIndex].Area);
                if (e.ColumnIndex == _colUsada.Index)
                {
                    string u = UsadaEm(_ios[e.RowIndex].Nome);
                    e.Value = u;
                    e.FormattingApplied = true;
                    e.CellStyle.ForeColor = u == "(não usada)" ? Tema.TextoFraco : Tema.TextoSec;
                }
            };
            _gridIos.CurrentCellDirtyStateChanged += delegate
            {
                if (_gridIos.IsCurrentCellDirty && _gridIos.CurrentCell is DataGridViewComboBoxCell)
                    _gridIos.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _gridIos.CellValueChanged += delegate (object s, DataGridViewCellEventArgs e)
            {
                if (_carregando || e.RowIndex < 0 || e.RowIndex >= _ios.Count || e.ColumnIndex < 0) return;
                string coluna = _gridIos.Columns[e.ColumnIndex].Name;
                Io io = _ios[e.RowIndex];
                if (coluna == "Area") io.Tipo = Io.TipoPadrao(io.Area, io.Numero, io.Tipo);
                else if (coluna == "Numero" && io.Area == "MEM") io.Tipo = Io.TipoPadrao("MEM", io.Numero, io.Tipo);
                else return;
                _gridIos.InvalidateRow(e.RowIndex);
            };
            AdicionarMenuExcluir(_gridIos);

            // barra: área das linhas novas, legenda das cores, resumo e localizar
            var barra = new Panel { Dock = DockStyle.Top, Height = 70, BackColor = Color.Transparent };
            Label lblArea = Tema.Secao("Área das linhas novas");
            lblArea.Location = new Point(0, 0);
            barra.Controls.Add(lblArea);
            _cmbAreaPadrao = Tema.Lista(false);
            _cmbAreaPadrao.Items.AddRange(new object[] { "DI", "DO", "AI", "AO", "MEM", "OUTRO" });
            _cmbAreaPadrao.SelectedIndex = 0;
            Control quadroArea = Tema.Emoldurar(_cmbAreaPadrao);
            quadroArea.SetBounds(0, 20, 150, Moldura.AlturaPadrao);
            barra.Controls.Add(quadroArea);

            _resumoTags = new FlowLayoutPanel { Location = new Point(168, 27), AutoSize = true, WrapContents = false, BackColor = Color.Transparent };
            foreach (string a in new[] { "DI", "DO", "AI", "AO", "MEM" })
                _resumoTags.Controls.Add(new Etiqueta(a) { CorFundo = CorDaArea(a, true), CorTexto = Tema.Texto, Tag = "area" });
            barra.Controls.Add(_resumoTags);

            _txtLocalizar = Tema.CaixaTexto();
            Control quadroLocalizar = Tema.Emoldurar(_txtLocalizar);
            quadroLocalizar.SetBounds(0, 20, 260, Moldura.AlturaPadrao);
            Label lblLocalizar = Tema.Secao("Localizar tag (Enter = próxima)");
            Action ancorar = delegate
            {
                quadroLocalizar.Left = barra.Width - quadroLocalizar.Width;
                lblLocalizar.Location = new Point(quadroLocalizar.Left, 0);
            };
            barra.Resize += delegate { ancorar(); };
            barra.Controls.Add(lblLocalizar);
            barra.Controls.Add(quadroLocalizar);
            _txtLocalizar.TextChanged += delegate { LocalizarTag(false); };
            _txtLocalizar.KeyDown += delegate (object o, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { e.Handled = e.SuppressKeyPress = true; LocalizarTag(true); }
            };

            cartao.Controls.Add(_gridIos);
            cartao.Controls.Add(barra);

            return NovaPagina("Tags de PLC", "Entradas · saídas · memórias",
                "Copie as tags no TIA Portal (tabela de tags) ou numa planilha e cole aqui com Ctrl+V: o gerador reconhece nome, endereço, tipo e descrição. "
                + "Com área e número preenchidos, a tag é criada no PLC (tabela \"" + Gerador.NomeGrupo + "\"). A coluna USADA EM mostra onde cada tag é usada.",
                new Control[]
                {
                    Tema.Botao("Colar tags  (Ctrl+V)", true, delegate { ColarTags(); }, "colar"),
                    Tema.Botao("Organizar por área", false, delegate { OrganizarPorArea(); }, "organizar"),
                    BotaoExcluir()
                }, cartao);
        }

        private static DataGridViewComboBoxColumn ColLista(string prop, string titulo, string[] itens, int peso)
        {
            var c = new DataGridViewComboBoxColumn
            {
                Name = prop,
                DataPropertyName = prop,
                HeaderText = titulo,
                FillWeight = peso,
                FlatStyle = FlatStyle.Flat,
                DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            c.Items.AddRange(itens);
            return c;
        }

        private string AreaPadrao()
        {
            string v = _cmbAreaPadrao != null ? _cmbAreaPadrao.SelectedItem as string : null;
            return string.IsNullOrEmpty(v) ? "DI" : v;
        }

        private static Color CorDaArea(string area, bool forte = false)
        {
            switch (area)
            {
                case "DI": return forte ? Color.FromArgb(18, 92, 78) : Color.FromArgb(13, 50, 54);
                case "DO": return forte ? Color.FromArgb(120, 84, 22) : Color.FromArgb(48, 44, 38);
                case "AI": return forte ? Color.FromArgb(22, 84, 168) : Color.FromArgb(15, 46, 88);
                case "AO": return forte ? Color.FromArgb(98, 60, 160) : Color.FromArgb(38, 36, 82);
                case "MEM": return forte ? Color.FromArgb(52, 78, 104) : Color.FromArgb(18, 42, 66);
                default: return Tema.Cartao;
            }
        }

        /// <summary>Etiquetas ao lado da legenda: tags sem endereço e não usadas.</summary>
        private void AtualizarResumoTags()
        {
            if (_resumoTags == null) return;
            foreach (Control c in _resumoTags.Controls.Cast<Control>().Where(x => !"area".Equals(x.Tag)).ToList()) c.Dispose();
            int total = _ios.Count(x => !IoVazia(x));
            int semEnd = _ios.Count(x => !Vazio(x.Nome) && Vazio(x.Endereco));
            int livres = _ios.Count(x => !Vazio(x.Nome) && UsadaEm(x.Nome) == "(não usada)");
            var sep = new Panel { Width = 14, Height = 22, BackColor = Color.Transparent, Margin = new Padding(0) };
            _resumoTags.Controls.Add(sep);
            _resumoTags.Controls.Add(new Etiqueta(Plural(total, "tag", "tags")));
            if (semEnd > 0)
                _resumoTags.Controls.Add(new Etiqueta("⚠ " + semEnd + " sem endereço") { CorFundo = Color.FromArgb(70, 52, 14), CorTexto = Tema.Amarelo });
            if (livres > 0)
                _resumoTags.Controls.Add(new Etiqueta(livres + (livres == 1 ? " não usada" : " não usadas")) { CorFundo = Color.FromArgb(20, 44, 68), CorTexto = Tema.TextoSec });
        }

        /// <summary>Vai para a próxima tag cujo nome, endereço ou descrição contém o texto.</summary>
        private void LocalizarTag(bool proxima)
        {
            string f = (_txtLocalizar.Text ?? "").Trim().ToLowerInvariant();
            if (f.Length == 0 || _ios.Count == 0) return;
            int ini = _gridIos.CurrentCell != null ? _gridIos.CurrentCell.RowIndex + (proxima ? 1 : 0) : 0;
            for (int k = 0; k < _ios.Count; k++)
            {
                int i = (ini + k) % _ios.Count;
                Io io = _ios[i];
                string texto = ((io.Nome ?? "") + " " + io.Endereco + " " + (io.Descricao ?? "")).ToLowerInvariant();
                if (!texto.Contains(f) || i >= _gridIos.Rows.Count) continue;
                _gridIos.ClearSelection();
                _gridIos.CurrentCell = _gridIos.Rows[i].Cells[0];
                _gridIos.Rows[i].Selected = true;
                return;
            }
        }

        private void OrganizarPorArea()
        {
            _gridIos.EndEdit();
            _gridIos.CurrentCell = null;
            List<Io> ordenadas = _ios.OrderBy(i => { int k = Array.IndexOf(Io.Areas, i.Area); return k < 0 ? 99 : k; }).ToList();
            _carregando = true;
            try
            {
                _ios.RaiseListChangedEvents = false;
                _ios.Clear();
                foreach (Io io in ordenadas) _ios.Add(io);
            }
            finally
            {
                _ios.RaiseListChangedEvents = true;
                _carregando = false;
            }
            ReligarTags();
            AoMudarDados();
        }

        /// <summary>Onde a tag esta sendo usada (para ver o que sobrou).</summary>
        private string UsadaEm(string nome)
        {
            if (Vazio(nome)) return "";
            nome = nome.Trim();
            var usos = new List<string>();
            foreach (Partida p in _partidas)
            {
                string id = p.Sub + "." + p.Eq + "." + p.Fn;
                if ((p.TagConf ?? "").Trim() == nome) usos.Add(id + " (confirmação)");
                if ((p.TagDefDrive ?? "").Trim() == nome) usos.Add(id + " (defeito do drive)");
                if ((p.TagConfFec ?? "").Trim() == nome) usos.Add(id + " (confirmação fechada)");
                if ((p.TagFec ?? "").Trim() == nome) usos.Add(id + " (saída fecha)");
                if ((p.TagPosAi ?? "").Trim() == nome) usos.Add(id + " (posição AI)");
                if ((p.TagHabC1 ?? "").Trim() == nome) usos.Add(id + " (habilita contator 1)");
                if ((p.TagHabC2 ?? "").Trim() == nome) usos.Add(id + " (habilita contator 2)");
                if ((p.TagRef ?? "").Trim() == nome) usos.Add(id + " (saída analógica)");
                if ((p.TagHab ?? "").Trim() == nome) usos.Add(id + " (habilita)");
                if (p.TagDefEfetiva == nome) usos.Add(id + " (defeito)");
                if ((p.TagProtecao ?? "").Trim() == nome) usos.Add(id + " (proteção)");
                if (!Vazio(p.TagProtecao) && p.TagProtecaoMemEfetiva == nome) usos.Add(id + " (memória da proteção)");
                if ((p.TagExaConf ?? "").Trim() == nome) usos.Add(id + " (confirmação exaustor)");
                if ((p.TagExaHab ?? "").Trim() == nome) usos.Add(id + " (habilita exaustor)");
                if (Gerador.TemExaustor(p) && Gerador.TagDefExa(_partidas, p) == nome) usos.Add(id + " (defeito exaustor)");
            }
            foreach (Partida p in _sensores)
            {
                string id = p.Sub + "." + p.Eq + "." + p.Fn;
                if ((p.TagSensor ?? "").Trim() == nome) usos.Add(id + " (sensor)");
                if ((p.TagPulso ?? "").Trim() == nome) usos.Add(id + " (totalizador)");
                if ((p.TagSensor2 ?? "").Trim() == nome) usos.Add(id + " (pá esquerda)");
                if ((p.TagConf ?? "").Trim() == nome) usos.Add(id + " (confirmação partida)");
            }
            return usos.Count == 0 ? "(não usada)" : string.Join(", ", usos.ToArray());
        }

        // ===================================================================
        //  Configuracao
        // ===================================================================
        private Control CriarPaginaConfig()
        {
            var host = new Panel { AutoScroll = true, BackColor = Tema.Fundo };
            Win.Escurecer(host);
            const int largura = 1040;
            int larguraCampo = (largura - 44) / 2 - 26;
            var coluna = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Tema.Fundo };

            _txtPrefixo = Tema.CaixaTexto();
            _txtReset = Tema.CaixaTexto();
            _txtEmerg = Tema.CaixaTexto();
            _txtRemLoc = Tema.CaixaTexto();
            _txtVersaoPid = Tema.CaixaTexto();
            _txtObPid = Tema.CaixaTexto();
            _chkPidNoOb = Tema.Marcador("Gerar os PIDs direto no OB de interrupção cíclica (sem FC G_PID)");
            _txtExport = Tema.CaixaTexto();
            _chkManter = Tema.Marcador("Manter os XMLs gerados na pasta temporária (diagnóstico)");
            _txtIniDef = Tema.CaixaTexto();
            _txtWordDef = Tema.CaixaTexto();
            _chkCom = Tema.Marcador("Gerar o mapa Modbus (DB COM e FC de mapeamento) no \"Gerar tudo\"");
            _chkComRecriar = Tema.Marcador("Se o DB e a FC do Modbus já existirem, apagar e gerar tudo de novo");
            _txtComDb = Tema.CaixaTexto();
            _txtComNum = Tema.CaixaTexto();
            _txtStatusWord = Tema.CaixaTexto();
            _txtIniStatus = Tema.CaixaTexto();
            _txtPrimeiroCiclo = Tema.CaixaTexto();
            _txtIniAux = Tema.CaixaTexto();
            _txtMbInst = Tema.CaixaTexto();
            _txtMbCon = Tema.CaixaTexto();
            _txtMbIface = Tema.CaixaTexto();
            _txtMbPorta = Tema.CaixaTexto();
            _txtClock = Tema.CaixaTexto();
            _txtPadraoConf = Tema.CaixaTexto(); _txtPadraoHab = Tema.CaixaTexto();
            _txtPadraoHabInv = Tema.CaixaTexto(); _txtPadraoHabSoft = Tema.CaixaTexto();
            _txtPadraoHabBomba1 = Tema.CaixaTexto(); _txtPadraoHabBomba2 = Tema.CaixaTexto();
            _txtPadraoDef = Tema.CaixaTexto(); _txtPadraoMedidor = Tema.CaixaTexto();
            _txtPadraoTotalizador = Tema.CaixaTexto(); _txtPadraoChave = Tema.CaixaTexto();
            _txtPadraoChaveMovimento = Tema.CaixaTexto();
            _chkAlocarDef = Tema.Marcador("Dar endereço automático às memórias de defeito");

            // cada seção é um cartão com os campos em duas colunas (rótulo em cima, explicação embaixo)
            CartaoTabela card = null;
            Action<string, string> secao = delegate (string titulo, string descricao)
            {
                card = new CartaoTabela(2) { Margin = new Padding(0, 0, 0, 14), MinimumSize = new Size(largura, 0), MaximumSize = new Size(largura, 0) };
                card.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 50);
                card.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 50);
                Label t = Tema.TituloCartao(titulo);
                t.Margin = new Padding(0, 0, 0, descricao != null ? 3 : 12);
                card.Controls.Add(t);
                card.SetColumnSpan(t, 2);
                if (descricao != null)
                {
                    Label d = Tema.Ajuda(descricao, largura - 60);
                    d.Margin = new Padding(0, 0, 0, 14);
                    card.Controls.Add(d);
                    card.SetColumnSpan(d, 2);
                }
                coluna.Controls.Add(card);
            };
            Action<string, Control, string, bool> add = delegate (string rotulo, Control ctl, string ajuda, bool inteiro)
            {
                var b = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, BackColor = Color.Transparent, Margin = new Padding(0, 0, 26, 16) };
                b.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                if (rotulo != null) b.Controls.Add(Tema.Secao(rotulo));
                Control quadro = Tema.Emoldurar(ctl);
                quadro.Dock = DockStyle.Fill;
                quadro.Margin = new Padding(0);
                b.Controls.Add(quadro);
                var a = Tema.Ajuda(ajuda, inteiro ? largura - 70 : larguraCampo);
                a.ForeColor = Tema.TextoFraco;
                a.Margin = new Padding(1, 4, 0, 0);
                b.Controls.Add(a);
                card.Controls.Add(b);
                if (inteiro) card.SetColumnSpan(b, 2);
            };

            secao("Blocos e sinais gerais", null);
            add("Prefixo dos blocos", _txtPrefixo, "Vai na frente de todo bloco gerado. Ex.: G_ → DB G_FL1, FC G_PARTIDAS_FL1, instâncias iG_...", false);
            add("Tag de reset", _txtReset, "Entrada de reset geral do painel (SIS_CMD_RST das partidas e do raspador). Ex.: RESET_PAINEL.", false);
            add("Tag de emergência", _txtEmerg, "Entra negada no SIS_LEI_EME das partidas. Ex.: SIS_EME_GER_ACI_CNF.", false);
            add("Contato remoto/local (visualização)", _txtRemLoc, "Contato da bobina SI<n>.VIS.OFF junto com o CMD_AUT. Ex.: GER.REM_LOC_CMD_SEL. Vazio ou DB inexistente = só CMD_AUT.", false);
            add("Bit de primeiro ciclo", _txtPrimeiroCiclo, "Usado no automático das máquinas (ex.: FirstScan, %M1.0). Vazio ou inexistente = sem o contato.", false);
            add("Clock de 1 Hz", _txtClock, "Usado no totalizador do medidor do preparador (pulso = água habilitada E clock). Ex.: Clock_1Hz (%M0.5 com a clock memory ativa).", false);

            secao("Defeitos e memórias", null);
            add(null, _chkAlocarDef, "Espelhos de defeito, memórias de proteção, defeito do exaustor e do raspador sem endereço "
                + "recebem o próximo bit livre (pula o que já está ocupado no PLC e na página Tags) e entram na página Tags de PLC.", true);
            add("Primeiro bit dos defeitos", _txtIniDef, "Ex.: 502.1 → M502.1, M502.2 ... M503.7, M504.0... O M502.0 fica reservado para a emergência geral.", false);
            add("Nome das words de defeito", _txtWordDef, "Ex.: DEFEITOS_ → DEFEITOS_1 (MW502), DEFEITOS_2 (MW504)... criadas se ainda não existirem.", false);
            add("Status words dos sensores", _txtStatusWord, "Nome das words com os bits de nível baixo/alto (padrão STATUS_WORD_ → STATUS_WORD_1, _2...).", false);
            add("Primeira status word (MW)", _txtIniStatus, "Ex.: 600 → STATUS_WORD_1 = MW600, STATUS_WORD_2 = MW602... (criadas se não existirem).", false);
            add("Primeira word auxiliar (MW)", _txtIniAux, "Memórias Int das redes (SET_AUX dos PIDs, SET_POS_AUX/LEI_POS_AUX das válvulas analógicas) "
                + "ganham a próxima %MW livre a partir daqui. Ex.: 100 → MW100, MW102...", false);

            secao("Comunicação Modbus", null);
            add(null, _chkCom, "CLP como servidor Modbus TCP. Monta o mapa (40001...) a partir das partidas e sensores: "
                + "comandos, escrita, leitura e DWords, no padrão da empresa. Veja na página Mapa Modbus.", true);
            add(null, _chkComRecriar, "Vale para o DB do mapa, a FC de mapeamento e os DBs de instância dos comandos, em qualquer pasta e mesmo feitos à mão. "
                + "Antes de apagar, o Genesis guarda uma cópia XML em %AppData%\\Genesis\\Backup. Desmarcado, só substitui o que já foi gerado pelo Genesis.", true);
            add("DB do mapa", _txtComDb, "Nome do DB dos holding registers (padrão COM).", false);
            add("Número do DB do mapa", _txtComNum, "Número fixo (padrão 6 → DB6). O MB_HOLD_REG aponta para P#DB6.DBX0.0.", false);
            add("Instância do MB_SERVER", _txtMbInst, "DB de instância do MB_SERVER. Se o projeto não tiver nenhuma chamada dele, o gerador cria (padrão MB_SERVER_DB).", false);
            add("Conexão do MB_SERVER", _txtMbCon, "DB.membro do TCON_IP_v4 ligado no CONNECT (padrão CNT.CONNECT). Criado se o DB não existir.", false);
            add("Interface PROFINET (HW ID)", _txtMbIface, "InterfaceId do TCON_IP_v4: identificador de hardware da porta PROFINET do CLP (Propriedades > Identificadores de hardware). Padrão 64.", false);
            add("Porta Modbus TCP", _txtMbPorta, "Porta local do servidor (padrão 502).", false);

            secao("PID, modelos e diagnóstico", null);
            add(null, _chkPidNoOb, "As redes de todos os PIDs do projeto são escritas dentro do OB (o tempo de ciclo e as outras propriedades do OB ficam). "
                + "Só o que está nas redes desse OB é refeito; uma cópia XML do OB antigo vai para %AppData%\\Genesis\\Backup. Desmarcado, cada subsistema ganha uma FC G_PID_<DB> para você chamar no OB.", true);
            add("Número do OB dos PIDs", _txtObPid, "Padrão 30 (Cyclic interrupt [OB30]). Se o OB não existir, o Genesis cria.", false);
            add("Versão do PID_Compact", _txtVersaoPid, "Vazio = automática (copia a de um PID que já esteja chamado num OB). Ex.: 2.4. Tem que ser a mesma em toda a CPU.", false);
            var linhaPasta = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0) };
            linhaPasta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            linhaPasta.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Control quadroPasta = Tema.Emoldurar(_txtExport);
            quadroPasta.Dock = DockStyle.Fill;
            quadroPasta.Margin = new Padding(0);
            linhaPasta.Controls.Add(quadroPasta, 0, 0);
            var btnPasta = Tema.Botao("Escolher…", false, delegate
            {
                using (var dlg = new FolderBrowserDialog())
                {
                    dlg.Description = "Pasta do export (FAST_Export_...) usada como modelo";
                    if (dlg.ShowDialog() == DialogResult.OK) _txtExport.Text = dlg.SelectedPath;
                }
            }, "abrir");
            btnPasta.MinimumSize = new Size(0, Moldura.AlturaPadrao);
            btnPasta.Margin = new Padding(8, 0, 0, 0);
            linhaPasta.Controls.Add(btnPasta, 1, 0);
            add("Pasta do export (modelos)", linhaPasta, "Reserva de modelos de DB de instância, para blocos que ainda não têm instância no projeto. Vazio = procura a pasta Templates automaticamente.", false);
            add(null, _chkManter, "Guarda os XMLs gerados para conferir em caso de erro de importação.", true);

            secao("Nomes automáticos das tags", "Modelo do nome que cada sinal de campo recebe quando a tag fica em branco. "
                + "Use {DESC} (descrição do item), {SUB}, {EQ} e {FN}.");
            add("Confirmação", _txtPadraoConf, "Tag de entrada de confirmação.", false);
            add("Habilitação", _txtPadraoHab, "Saída de habilitação para partida direta e diafragma.", false);
            add("Habilitação do inversor", _txtPadraoHabInv, "Habilitação do inversor em partida com bypass.", false);
            add("Habilitação da soft-starter", _txtPadraoHabSoft, "Habilitação da soft-starter em partida com bypass.", false);
            add("Bypass — bomba 1", _txtPadraoHabBomba1, "Habilitação do contator/bomba 1 no bypass.", false);
            add("Bypass — bomba 2", _txtPadraoHabBomba2, "Habilitação do contator/bomba 2 no bypass.", false);
            add("Defeito", _txtPadraoDef, "Entrada de defeito do inversor/soft-starter.", false);
            add("Medidor", _txtPadraoMedidor, "Entrada analógica de medidores de vazão, pH, nível etc. Padrão: MEDIDOR_{DESC} (nome completo, como nas partidas).", false);
            add("Totalizador", _txtPadraoTotalizador, "Entrada de pulso do totalizador de vazão.", false);
            add("Chave", _txtPadraoChave, "Chaves digitais, como nível alto/baixo.", false);
            add("Movimento", _txtPadraoChaveMovimento, "Sensores de movimento das pás do flotador/raspador.", false);

            host.Controls.Add(coluna);
            return NovaPagina("Configuração", "Nomes e opções da geração",
                "Valem para todo o projeto e ficam salvas neste computador. Em dúvida, deixe como está: os padrões seguem os projetos existentes.",
                null, host);
        }

        // ===================================================================
        //  Log (gaveta de baixo) e linha de status
        // ===================================================================
        private void Log(string texto)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(Log), texto);
                return;
            }
            string t = texto ?? "";
            string tt = t.TrimStart();
            Color cor = Tema.Verde;
            if (tt.StartsWith("ERRO") || tt.StartsWith("FALHA") || tt.StartsWith("ATENCAO")) cor = Tema.Vermelho;
            else if (tt.StartsWith("AVISO") || tt.StartsWith("Corrija") || tt.StartsWith("- ") || tt.StartsWith("Obs")) cor = Tema.Amarelo;
            else if (tt.StartsWith(">>>")) cor = Tema.Ciano;
            else if (tt.StartsWith("INFO") || tt.StartsWith("Importando") || tt.StartsWith("Modelo")) cor = Tema.TextoSec;

            _txtLog.SelectionStart = _txtLog.TextLength;
            _txtLog.SelectionLength = 0;
            _txtLog.SelectionColor = cor;
            _txtLog.AppendText(t + Environment.NewLine);
            _txtLog.SelectionColor = _txtLog.ForeColor;
            _txtLog.ScrollToCaret();

            if (tt.Length > 0 && _lblStatus != null)
            {
                _lblStatus.Text = tt.Split('\r', '\n')[0];
                _lblStatus.ForeColor = cor == Tema.Verde ? Tema.TextoSec : cor;
            }
        }

        internal void RegistrarErroDeTela(Exception ex)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<Exception>(RegistrarErroDeTela), ex);
                return;
            }
            Log("");
            MostrarLog(true);
            Log("ERRO NA TELA (o programa continua): " + ex.Message);
            foreach (string linha in (ex.StackTrace ?? "").Split('\n').Take(8)) Log("   " + linha.Trim());
            Log("Se repetir, me mande estas linhas.");
        }

        // ===================================================================
        //  Abertura / fechamento (guarda a ultima sessao)
        // ===================================================================
        private void AoAbrir(string arquivoInicial)
        {
            _txtPrefixo.Text = Cfg.Prefixo;
            _txtReset.Text = Cfg.TagReset;
            _txtEmerg.Text = Cfg.TagEmergencia;
            _txtRemLoc.Text = Cfg.ContatoRemLoc;
            _txtExport.Text = Cfg.PastaExport;
            _txtVersaoPid.Text = Cfg.VersaoPid;
            _chkPidNoOb.Checked = Cfg.PidNoOb;
            _txtObPid.Text = Cfg.NumeroObPid;
            _chkManter.Checked = Cfg.ManterXml;
            _chkAlocarDef.Checked = Cfg.AlocarDefeitos;
            _txtIniDef.Text = Cfg.InicioDefeitos;
            _txtWordDef.Text = Cfg.NomeWordDefeitos;
            _chkCom.Checked = Cfg.ComAtivo;
            _chkComRecriar.Checked = Cfg.ComRecriar;
            _txtComDb.Text = Cfg.ComDb;
            _txtComNum.Text = Cfg.ComNumero;
            _txtStatusWord.Text = Cfg.NomeStatusWord;
            _txtIniStatus.Text = Cfg.InicioStatusWord;
            _txtPrimeiroCiclo.Text = Cfg.TagPrimeiroCiclo;
            _txtIniAux.Text = Cfg.InicioAux;
            _txtMbInst.Text = Cfg.MbServerDb;
            _txtMbCon.Text = Cfg.ConexaoModbus;
            _txtMbIface.Text = Cfg.InterfaceModbus;
            _txtMbPorta.Text = Cfg.PortaModbus;
            _txtClock.Text = Cfg.TagClock1Hz;
            _txtPadraoConf.Text = Cfg.PadraoConf; _txtPadraoHab.Text = Cfg.PadraoHab;
            _txtPadraoHabInv.Text = Cfg.PadraoHabInv; _txtPadraoHabSoft.Text = Cfg.PadraoHabSoft;
            _txtPadraoHabBomba1.Text = Cfg.PadraoHabBomba1; _txtPadraoHabBomba2.Text = Cfg.PadraoHabBomba2;
            _txtPadraoDef.Text = Cfg.PadraoDef; _txtPadraoMedidor.Text = Cfg.PadraoMedidor;
            _txtPadraoTotalizador.Text = Cfg.PadraoTotalizador; _txtPadraoChave.Text = Cfg.PadraoChave;
            _txtPadraoChaveMovimento.Text = Cfg.PadraoChaveMovimento;

            bool abriu = false;
            if (!Vazio(arquivoInicial))
            {
                try
                {
                    CarregarProjeto(Projeto.Abrir(arquivoInicial));
                    _arquivoAtual = Path.GetFullPath(arquivoInicial);
                    abriu = true;
                    Log("Projeto aberto: " + _arquivoAtual);
                }
                catch (Exception ex)
                {
                    Log("ERRO: não foi possível abrir " + arquivoInicial + ": " + ex.Message);
                }
            }
            if (!abriu)
            {
                try
                {
                    // última sessão (a versão antiga guardava em ultimo.fastproj)
                    string ultimo = File.Exists(ArquivoUltimo) ? ArquivoUltimo : Path.Combine(Cfg.PastaApp, "ultimo.fastproj");
                    if (File.Exists(ultimo))
                    {
                        CarregarProjeto(Projeto.Abrir(ultimo));
                        if (!Vazio(Cfg.UltimoArquivo) && File.Exists(Cfg.UltimoArquivo)) _arquivoAtual = Cfg.UltimoArquivo;
                    }
                }
                catch
                {
                    // sessão anterior ilegível: começa em branco
                }
            }
            _modificado = false;
            AtualizarTitulo();
            AtualizarContadoresAgora();

            if (!abriu)
                Log(_partidas.Count + _sensores.Count + _pids.Count + _ios.Count == 0
                    ? "Pronto. Comece pelas tags (etapa 1) ou crie uma máquina do catálogo (etapa 2)."
                    : "Pronto. Sessão anterior restaurada.");
        }

        private void AoFechar()
        {
            LerConfigDaTela();
            Cfg.UltimoArquivo = _arquivoAtual ?? "";
            Cfg.Salvar();
            try
            {
                FinalizarEdicao();
                Projeto.Salvar(ArquivoUltimo, MontarProjeto());
            }
            catch
            {
                // nao impede o fechamento
            }
        }

        private void FinalizarEdicao()
        {
            foreach (Cadastro c in new[] { _cadPart, _cadSen, _cadPid, _cadMaq }) c.Grade.EndEdit();
            _gridIos.EndEdit();
        }

        private string ArquivoUltimo
        {
            get { return Path.Combine(Cfg.PastaApp, "ultimo" + App.Extensao); }
        }

        private void LerConfigDaTela()
        {
            Cfg.Prefixo = _txtPrefixo.Text.Trim();
            Cfg.TagReset = _txtReset.Text.Trim();
            Cfg.TagEmergencia = _txtEmerg.Text.Trim();
            Cfg.ContatoRemLoc = _txtRemLoc.Text.Trim();
            Cfg.PastaExport = _txtExport.Text.Trim();
            Cfg.VersaoPid = _txtVersaoPid.Text.Trim();
            Cfg.PidNoOb = _chkPidNoOb.Checked;
            Cfg.NumeroObPid = _txtObPid.Text.Trim();
            Cfg.ManterXml = _chkManter.Checked;
            Cfg.AlocarDefeitos = _chkAlocarDef.Checked;
            Cfg.InicioDefeitos = _txtIniDef.Text.Trim();
            Cfg.NomeWordDefeitos = _txtWordDef.Text.Trim();
            Cfg.ComAtivo = _chkCom.Checked;
            Cfg.ComRecriar = _chkComRecriar.Checked;
            Cfg.ComDb = _txtComDb.Text.Trim();
            Cfg.ComNumero = _txtComNum.Text.Trim();
            Cfg.NomeStatusWord = _txtStatusWord.Text.Trim();
            Cfg.InicioStatusWord = _txtIniStatus.Text.Trim();
            Cfg.TagPrimeiroCiclo = _txtPrimeiroCiclo.Text.Trim();
            Cfg.InicioAux = _txtIniAux.Text.Trim();
            Cfg.MbServerDb = _txtMbInst.Text.Trim();
            Cfg.ConexaoModbus = _txtMbCon.Text.Trim();
            Cfg.InterfaceModbus = _txtMbIface.Text.Trim();
            Cfg.PortaModbus = _txtMbPorta.Text.Trim();
            Cfg.TagClock1Hz = _txtClock.Text.Trim();
            Cfg.PadraoConf = _txtPadraoConf.Text.Trim(); Cfg.PadraoHab = _txtPadraoHab.Text.Trim();
            Cfg.PadraoHabInv = _txtPadraoHabInv.Text.Trim(); Cfg.PadraoHabSoft = _txtPadraoHabSoft.Text.Trim();
            Cfg.PadraoHabBomba1 = _txtPadraoHabBomba1.Text.Trim(); Cfg.PadraoHabBomba2 = _txtPadraoHabBomba2.Text.Trim();
            Cfg.PadraoDef = _txtPadraoDef.Text.Trim(); Cfg.PadraoMedidor = _txtPadraoMedidor.Text.Trim();
            Cfg.PadraoTotalizador = _txtPadraoTotalizador.Text.Trim(); Cfg.PadraoChave = _txtPadraoChave.Text.Trim();
            Cfg.PadraoChaveMovimento = _txtPadraoChaveMovimento.Text.Trim();
        }

        // ===================================================================
        //  Projeto (abrir / salvar / importar CSV)
        // ===================================================================
        private Projeto MontarProjeto()
        {
            var p = new Projeto();
            p.Ios = _ios.Where(i => !IoVazia(i)).ToList();
            p.Partidas = _partidas.Concat(_sensores).Concat(_pids).Where(x => !PartidaVazia(x)).ToList();
            p.Hardware = _projetoHardware;
            return p;
        }

        private void CarregarProjeto(Projeto p)
        {
            _carregando = true;
            if (_gridIos != null) { _gridIos.EndEdit(); _gridIos.CurrentCell = null; }
            try
            {
                // a grade das tags só é avisada no fim (uma vez), como no "Organizar por área"
                _ios.RaiseListChangedEvents = false;
                _ios.Clear();
                foreach (Io io in p.Ios) _ios.Add(io);
                _ios.RaiseListChangedEvents = true;
                ReligarTags();
                _partidas.Clear();
                _sensores.Clear();
                _pids.Clear();
                foreach (Partida pa in p.Partidas)
                {
                    pa.Tipo = Gerador.NormalizarTipo(pa.Tipo);   // arquivos antigos: hibrida -> Partida direta
                    if (Gerador.EhPid(pa)) _pids.Add(pa);
                    else if (Gerador.EhSensor(pa)) { PreencherAutomaticos(pa); _sensores.Add(pa); }
                    else { PreencherAutomaticos(pa); _partidas.Add(pa); }
                }
            }
            finally
            {
                _ios.RaiseListChangedEvents = true;
                _carregando = false;
            }
            _projetoHardware = p.Hardware;
            if (_pagHardware != null) CarregarHardwareNaTela();
            foreach (Cadastro c in new[] { _cadPart, _cadSen, _cadPid })
            {
                c.Busca.Text = "";
                if (c.Filtro.Items.Count > 0) c.Filtro.SelectedIndex = 0;
                c.Editando = null;
                MontarEditor(c, true);
            }
            if (_paginaAtual == _pagMaquina) MostrarPagina(_pagMaquinas);
            AoMudarDados();
            AtualizarContadoresAgora();
            _modificado = false;
            AtualizarTitulo();
            if (_sensores.Count > 0 && NomesAntigosDeSensores().Count > 0) OferecerNomesCompletosDeSensores();
        }

        /// <summary>Avisa a grade das tags de que a lista mudou inteira.</summary>
        private void ReligarTags()
        {
            if (Tema.NoWindows || _gridIos == null) { _ios.ResetBindings(); return; }
            // fora do Windows (Mono), a linha "nova" da grade se perde no reset: desliga durante a troca
            _gridIos.AllowUserToAddRows = false;
            try { _ios.ResetBindings(); }
            finally { _gridIos.AllowUserToAddRows = true; }
        }

        private void MarcarModificado()
        {
            if (_modificado || _carregando) return;
            _modificado = true;
            AtualizarTitulo();
        }

        private void Novo()
        {
            bool temDados = _partidas.Count + _sensores.Count + _pids.Count + _ios.Count(x => !IoVazia(x)) > 0;
            if (temDados && !_confirmar("Começar um projeto novo, em branco?\n\n"
                + (_modificado ? "As alterações que não foram salvas em arquivo serão perdidas." : "O projeto atual já está salvo em arquivo."))) return;
            CarregarProjeto(new Projeto());
            _arquivoAtual = null;
            AtualizarTitulo();
            MostrarPagina(_pagInicio);
            Log("Projeto novo. Comece pelas tags (etapa 1) ou por uma máquina do catálogo (etapa 2).");
        }

        private void AbrirPastaApp()
        {
            try { System.Diagnostics.Process.Start(Cfg.PastaApp); }
            catch (Exception ex) { Dialogo.Erro(this, "Não foi possível abrir a pasta:\n" + ex.Message); }
        }

        private void Abrir()
        {
            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = App.FiltroArquivos;
                if (_arquivoAtual != null) dlg.InitialDirectory = Path.GetDirectoryName(_arquivoAtual);
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    CarregarProjeto(Projeto.Abrir(dlg.FileName));
                    _arquivoAtual = dlg.FileName;
                    AtualizarTitulo();
                    Log("Projeto aberto: " + dlg.FileName);
                }
                catch (Exception ex)
                {
                    Dialogo.Erro(this, "Não foi possível abrir o arquivo:\n" + ex.Message);
                }
            }
        }

        private void Salvar(bool comoNovo)
        {
            FinalizarEdicao();
            string arquivo = _arquivoAtual;
            if (comoNovo || arquivo == null)
            {
                using (var dlg = new SaveFileDialog())
                {
                    dlg.Filter = "Projeto Genesis (*.genesis)|*.genesis|Projeto no formato antigo (*.fastproj)|*.fastproj";
                    dlg.DefaultExt = App.Extensao.TrimStart('.');
                    dlg.FileName = arquivo != null ? Path.GetFileNameWithoutExtension(arquivo) + App.Extensao : "meu_projeto" + App.Extensao;
                    if (arquivo != null) dlg.InitialDirectory = Path.GetDirectoryName(arquivo);
                    if (dlg.ShowDialog() != DialogResult.OK) return;
                    arquivo = dlg.FileName;
                }
            }
            try
            {
                Projeto.Salvar(arquivo, MontarProjeto());
                _arquivoAtual = arquivo;
                _modificado = false;
                AtualizarTitulo();
                Log("Projeto salvo: " + arquivo);
            }
            catch (Exception ex)
            {
                Dialogo.Erro(this, "Não foi possível salvar:\n" + ex.Message);
            }
        }

        private void AtualizarTitulo()
        {
            string nome = _arquivoAtual != null ? Path.GetFileName(_arquivoAtual) : "Projeto sem nome";
            Text = App.Nome + "  —  " + nome + (_modificado ? " •" : "");
            _lblArquivo.Text = nome + (_modificado ? "  •  não salvo" : "");
            if (_lblProjetoInicio != null) _lblProjetoInicio.Text = nome;
            _dicas.SetToolTip(_lblArquivo, _arquivoAtual ?? "Ainda não foi salvo em arquivo (a sessão é guardada ao fechar)");
        }

        private void ImportarCsv()
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "Pasta com ios.csv e partidas.csv";
                if (dlg.ShowDialog() != DialogResult.OK) return;

                string arqIos = Path.Combine(dlg.SelectedPath, "ios.csv");
                string arqPart = Path.Combine(dlg.SelectedPath, "partidas.csv");
                if (!File.Exists(arqIos) || !File.Exists(arqPart))
                {
                    Dialogo.Aviso(this, "A pasta precisa ter ios.csv e partidas.csv.");
                    return;
                }
                try
                {
                    var p = new Projeto();
                    p.Ios = Csv.LerIos(arqIos);
                    p.Partidas = Csv.LerPartidas(arqPart);
                    foreach (Io io in p.Ios)
                    {
                        string t = TiposDados.FirstOrDefault(x => string.Equals(x, io.Tipo, StringComparison.OrdinalIgnoreCase));
                        if (t != null) io.Tipo = t;
                    }
                    foreach (Partida pa in p.Partidas)
                    {
                        string t = Gerador.TiposSuportados.FirstOrDefault(x => string.Equals(x, pa.Tipo, StringComparison.OrdinalIgnoreCase));
                        if (t != null) pa.Tipo = t;
                    }
                    CarregarProjeto(p);
                    Log("CSV importado: " + p.Ios.Count + " IOs, " + p.Partidas.Count + " partidas.");
                }
                catch (Exception ex)
                {
                    Dialogo.Erro(this, "Erro ao importar:\n" + ex.Message);
                }
            }
        }

        // ===================================================================
        //  Excluir, colar tags, lote
        // ===================================================================
        /// <summary>Confirmacao (troca-se nos testes automaticos).</summary>
        private Func<string, bool> _confirmar = delegate (string mensagem)
        {
            return Dialogo.Confirmar(Atual, mensagem);
        };

        private void AdicionarMenuExcluir(DataGridView grade)
        {
            var menu = MenuEscuro.Novo();
            menu.Items.Add("Excluir linha(s) selecionada(s)", null, delegate { ExcluirLinhas(); });
            grade.ContextMenuStrip = menu;
            grade.CellMouseDown += delegate (object s, DataGridViewCellMouseEventArgs e)
            {
                if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
                bool jaSelecionada = grade.Rows[e.RowIndex].Selected || (e.ColumnIndex >= 0 && grade[e.ColumnIndex, e.RowIndex].Selected);
                if (jaSelecionada) return;
                grade.ClearSelection();
                if (e.ColumnIndex >= 0) grade.CurrentCell = grade[e.ColumnIndex, e.RowIndex];
                grade.Rows[e.RowIndex].Selected = true;
            };
        }

        private void ExcluirLinhas()
        {
            Cadastro cad = CadastroDaPagina(_paginaAtual);
            DataGridView grade = cad != null ? cad.Grade : (_paginaAtual == _pagTags ? _gridIos : null);
            if (grade == null) return;
            grade.EndEdit();

            var indices = new SortedSet<int>();
            foreach (DataGridViewRow r in grade.SelectedRows) if (!r.IsNewRow) indices.Add(r.Index);
            foreach (DataGridViewCell c in grade.SelectedCells) if (!c.OwningRow.IsNewRow) indices.Add(c.RowIndex);
            if (indices.Count == 0 && grade.CurrentCell != null && !grade.CurrentCell.OwningRow.IsNewRow) indices.Add(grade.CurrentCell.RowIndex);

            if (indices.Count == 0)
            {
                Dialogo.Info(this, "Selecione uma ou mais linhas e tente de novo.");
                return;
            }

            string oque = cad != null ? cad.Nome + "(s)" : "tag(s)";
            if (!_confirmar("Excluir " + indices.Count + " " + oque + "?")) return;

            if (cad == null)
            {
                int emUso = indices.Count(i => i < _ios.Count && UsadaEm(_ios[i].Nome) != "(não usada)");
                if (emUso > 0 && !_confirmar(emUso + " das tags selecionadas está(ão) em uso. Excluir mesmo assim?")) return;
            }

            _carregando = true;
            try
            {
                foreach (int i in indices.Reverse())
                {
                    if (cad != null) { if (i < cad.Lista.Count) cad.Lista.RemoveAt(i); }
                    else if (i < _ios.Count) _ios.RemoveAt(i);
                }
            }
            finally
            {
                _carregando = false;
            }
            if (cad != null) MontarEditor(cad, true);
            AoMudarDados();
            Log("Excluída(s) " + indices.Count + " linha(s).");
        }

        private void ColarTags()
        {
            string texto;
            try { texto = Clipboard.GetText(); }
            catch { texto = ""; }

            if (Vazio(texto))
            {
                Dialogo.Info(this, "Não há texto para colar.\n\nCopie as tags no TIA Portal (tabela de tags do PLC) ou na planilha e tente de novo.");
                return;
            }

            var ignoradas = new List<string>();
            string areaPadrao = AreaPadrao();
            List<Io> lidas = LeitorTags.Interpretar(texto, ignoradas, areaPadrao);
            if (lidas.Count == 0)
            {
                Log("Colar: nenhuma tag reconhecida no texto copiado.");
                foreach (string ig in ignoradas.Take(5)) Log("  ignorada: " + ig);
                Dialogo.Aviso(this, "Não consegui reconhecer tags no texto copiado. Veja o status.");
                return;
            }

            int novas = 0, atualizadas = 0, iguais = 0;
            _carregando = true;
            try
            {
                foreach (Io n in lidas)
                {
                    Io atual = _ios.FirstOrDefault(x => x.Nome != null && x.Nome.Trim() == n.Nome);
                    if (atual == null)
                    {
                        if (Vazio(n.Endereco) && Vazio(n.Numero)) n.Area = areaPadrao ?? "DI";
                        if (Vazio(n.Tipo))
                            n.Tipo = n.Area == "OUTRO" ? LeitorTags.InferirTipo(n.Endereco) : Io.TipoPadrao(n.Area, n.Numero, "");
                        _ios.Add(n);
                        novas++;
                        continue;
                    }
                    bool mudou = false;
                    if (!Vazio(n.Endereco) && atual.Endereco != n.Endereco) { atual.Endereco = n.Endereco; mudou = true; }
                    if (!Vazio(n.Tipo) && atual.Tipo != n.Tipo) { atual.Tipo = n.Tipo; mudou = true; }
                    if (!Vazio(n.Descricao) && atual.Descricao != n.Descricao) { atual.Descricao = n.Descricao; mudou = true; }
                    if (mudou) atualizadas++; else iguais++;
                }
            }
            finally
            {
                _carregando = false;
            }

            ReligarTags();
            AoMudarDados();
            MostrarPagina(_pagTags);

            Log("Colar: " + lidas.Count + " tag(s) lida(s) - " + novas + " nova(s), " + atualizadas + " atualizada(s), " + iguais + " igual(is)"
                + (ignoradas.Count > 0 ? ", " + ignoradas.Count + " linha(s) ignorada(s)" : "") + ".");
            foreach (string ig in ignoradas.Take(5)) Log("  ignorada: " + ig);
            int semEnd = _ios.Count(x => !Vazio(x.Nome) && Vazio(x.Endereco));
            if (semEnd > 0) Log("Obs.: " + semEnd + " tag(s) sem endereço - preencha antes de gerar.");
        }

        private void AdicionarLote()
        {
            Partida ult = _cadPart.Editando ?? _partidas.LastOrDefault();
            using (var dlg = new LoteForm(ult != null ? ult.Sub : "", ult != null ? ult.Eq : ""))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                foreach (Partida p in dlg.Partidas) { PreencherAutomaticos(p); _partidas.Add(p); }
                foreach (Io io in dlg.NovasIos) if (!_ios.Any(x => x.Nome == io.Nome)) _ios.Add(io);
                if (dlg.Partidas.Count > 0) { Selecionar(_cadPart, dlg.Partidas[0]); MontarEditor(_cadPart, true); }
                AoMudarDados();
                Log("Adicionadas " + dlg.Partidas.Count + " partidas"
                    + (dlg.NovasIos.Count > 0 ? " e " + dlg.NovasIos.Count + " tags (preencha os endereços na página Tags de PLC)." : "."));
            }
        }

        // ===================================================================
        //  Gerar
        // ===================================================================
        private List<Partida> PartidasSelecionadas(List<Partida> validas)
        {
            var conjunto = new HashSet<Partida>(validas);
            return _partidas.Concat(_sensores).Concat(_pids).Where(p => p.Marcada && conjunto.Contains(p)).ToList();
        }

        private void Gerar(bool soSelecionadas)
        {
            Gerar(soSelecionadas, false);
        }

        private void Gerar(bool soSelecionadas, bool soComunicacao)
        {
            Gerar(soSelecionadas, soComunicacao, null);
        }

        private void SincronizarTagsDeCampo(IEnumerable<Partida> itens)
        {
            var definicoes = new Dictionary<string, Tuple<string, string>>
            {
                { "TagConf", Tuple.Create("DI", "Bool") }, { "TagConfFec", Tuple.Create("DI", "Bool") },
                { "TagHab", Tuple.Create("DO", "Bool") }, { "TagHabC1", Tuple.Create("DO", "Bool") },
                { "TagHabC2", Tuple.Create("DO", "Bool") }, { "TagDefDrive", Tuple.Create("DI", "Bool") },
                { "TagFec", Tuple.Create("DO", "Bool") }, { "TagPosAi", Tuple.Create("AI", "Int") },
                { "TagPulso", Tuple.Create("DI", "Bool") }, { "TagSensor2", Tuple.Create("DI", "Bool") },
                { "TagProtecao", Tuple.Create("DI", "Bool") }, { "TagExaConf", Tuple.Create("DI", "Bool") },
                { "TagExaHab", Tuple.Create("DO", "Bool") }
            };
            var existentes = new HashSet<string>(_ios.Where(x => !Vazio(x.Nome)).Select(x => x.Nome), StringComparer.OrdinalIgnoreCase);
            _carregando = true;
            try
            {
                foreach (Partida p in itens)
                {
                    PreencherAutomaticos(p);
                    foreach (var def in definicoes)
                    {
                        PropertyInfo pi = typeof(Partida).GetProperty(def.Key);
                        string nome = pi == null ? "" : Convert.ToString(pi.GetValue(p, null) ?? "").Trim();
                        if (nome.Length == 0 || !existentes.Add(nome)) continue;
                        _ios.Add(new Io { Nome = nome, Area = def.Value.Item1, Tipo = def.Value.Item2,
                            Descricao = p.Descricao + " — " + def.Key.Replace("Tag", "") });
                    }
                    Gerador.TipoPartida tipo = Gerador.BuscarTipo(p.Tipo);
                    if (tipo != null && (tipo.SenAna || tipo.SenVazao) && !Vazio(p.TagSensor) && existentes.Add(p.TagSensor.Trim()))
                        _ios.Add(new Io { Nome = p.TagSensor.Trim(), Area = "AI", Tipo = "Int", Descricao = p.Descricao + " — Medição analógica" });
                    else if (tipo != null && tipo.Sensor && !tipo.SenAna && !tipo.SenVazao && !Vazio(p.TagSensor) && existentes.Add(p.TagSensor.Trim()))
                        _ios.Add(new Io { Nome = p.TagSensor.Trim(), Area = "DI", Tipo = "Bool", Descricao = p.Descricao + " — Chave/sensor" });
                    if (!Vazio(p.TagRef) && existentes.Add(p.TagRef.Trim()))
                        _ios.Add(new Io { Nome = p.TagRef.Trim(), Area = "AO", Tipo = "Int", Descricao = p.Descricao + " — Referência analógica" });
                }
            }
            finally { _carregando = false; }
            ReligarTags();
        }

        private async void Gerar(bool soSelecionadas, bool soComunicacao, List<Partida> forcadas)
        {
            FinalizarEdicao();
            LerConfigDaTela();

            if (!Regex.IsMatch(Cfg.Prefixo, "^[A-Za-z0-9_]*$"))
            {
                MostrarPagina(_pagConfig);
                Dialogo.Aviso(this, "O prefixo dos blocos só pode ter letras, números e _.");
                return;
            }

            for (int i = 0; i < _partidas.Count; i++) { _partidas[i].Linha = i + 1; _partidas[i].Aba = "Partidas"; }
            for (int i = 0; i < _sensores.Count; i++) { _sensores[i].Linha = i + 1; _sensores[i].Aba = "Sensores"; }
            for (int i = 0; i < _pids.Count; i++) { _pids[i].Linha = i + 1; _pids[i].Aba = "PIDs"; _pids[i].Tipo = "PID"; }
            for (int i = 0; i < _ios.Count; i++) _ios[i].Linha = i + 1;

            List<Partida> partidas = _partidas.Concat(_sensores).Concat(_pids).Where(x => !PartidaVazia(x)).ToList();
            foreach (Partida p in partidas) PreencherAutomaticos(p);
            SincronizarTagsDeCampo(partidas);
            for (int i = 0; i < _ios.Count; i++) _ios[i].Linha = i + 1;
            List<Io> ios = _ios.Where(x => !IoVazia(x)).ToList();

            if (partidas.Count == 0)
            {
                Dialogo.Info(this, "Nenhuma partida, sensor ou PID cadastrado.");
                return;
            }

            List<Partida> selecionadas = null;
            if (soSelecionadas)
            {
                selecionadas = forcadas != null ? forcadas.Where(partidas.Contains).ToList() : PartidasSelecionadas(partidas);
                if (selecionadas.Count == 0)
                {
                    Dialogo.Info(this, "Nenhum item marcado.\n\nMarque a caixinha GERAR das partidas, sensores ou PIDs que quer gerar e tente de novo.");
                    return;
                }
            }

            foreach (Partida p in partidas) { p.Sub = (p.Sub ?? "").Trim(); p.Eq = (p.Eq ?? "").Trim(); p.Fn = (p.Fn ?? "").Trim(); }

            List<string> erros = Gerador.Validar(ios, partidas);
            if (erros.Count > 0)
            {
                Log("");
                Log("Corrija antes de gerar:");
                foreach (string e in erros) Log("  - " + e);
                MostrarPagina(_pagGerar);
                AtualizarConferencia();
                Dialogo.Aviso(this, "Há " + Plural(erros.Count, "problema", "problemas") + " para corrigir antes de gerar.\n\n"
                    + "Eles estão listados na conferência: clique em cada um para ir até o item.");
                return;
            }

            string aviso = soComunicacao
                ? "Vai gerar só a comunicação Modbus: o DB " + Cfg.ComDb + " (DB" + Cfg.ComNumero + ") é recriado com o mapa atual "
                    + "e a FC " + Cfg.Prefixo + "COMANDO_REMOTO é refeita"
                    + (Cfg.ComRecriar ? " (se já existirem, são apagados e gerados de novo, com cópia de segurança)" : "") + ". As partidas não são alteradas.\n\n"
                    + "Se o mapa mudou (itens novos no meio da lista), avise a equipe do supervisório.\n\nContinuar?"
                : soSelecionadas
                ? (forcadas != null ? "Vai gerar a máquina " + _maqAtual + " (" + selecionadas.Count + " itens). O resto do projeto é ignorado.\n\n"
                                    : "Vai gerar só os " + selecionadas.Count + " item(ns) marcado(s). Os outros são ignorados.\n\n")
                    + "• A FC que já existe NÃO é alterada: as redes vão para uma FC nova (..._NOVAS),\n  para você copiar para a sua FC no TIA.\n"
                    + "• No DB do subsistema, só é acrescentado o que falta.\n"
                    + "• Os DBs de instância que faltam são criados.\n\nContinuar?"
                : "Gerar TUDO recria os blocos de cada subsistema (DB, FC e instâncias) no grupo \"" + Gerador.NomeGrupo + "\".\n\n"
                    + "Qualquer edição manual feita nesses blocos no TIA (por exemplo, o contato de intertravamento já preenchido) será PERDIDA.\n\n"
                    + "Para gerar só alguns itens sem mexer no resto, marque a caixinha GERAR deles e escolha \"Só os itens marcados\".\n\nContinuar?";
            if (!_confirmar(aviso)) return;

            Habilitar(false);
            _txtLog.Clear();
            MostrarLog(true);
            MostrarResultado(null, "Gerando no TIA Portal… acompanhe o log abaixo.");
            DateTime inicio = DateTime.Now;
            bool? ok = null;
            try
            {
                var gerador = new Gerador(Log);
                gerador.AoCriarTags = delegate (List<Io> novas) { BeginInvoke(new Action(delegate { ReceberTags(novas); })); };
                gerador.SoComunicacao = soComunicacao;
                ok = await Task.Run(() => gerador.Executar(ios, partidas, selecionadas));
            }
            catch (Exception ex)
            {
                Log("");
                Log("ERRO:");
                Log(ex.ToString());
            }
            finally
            {
                Habilitar(true);
            }
            int seg = (int)Math.Round((DateTime.Now - inicio).TotalSeconds);
            if (ok == true) MostrarResultado(true, "✓ Geração concluída em " + seg + " s. Confira o log e compile no TIA Portal.");
            else MostrarResultado(false, "✗ A geração não foi concluída. Veja o motivo no log.");
            AtualizarConferencia();
        }

        // ===================================================================
        //  Mapa Modbus
        // ===================================================================
        private Control CriarPaginaModbus()
        {
            var host = new Panel { BackColor = Tema.Fundo };

            var topo = new Panel { Dock = DockStyle.Top, Height = 114, BackColor = Tema.Fundo };
            _resumoModbus = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 90, WrapContents = false, BackColor = Tema.Fundo };
            _avisoModbus = Tema.Ajuda("");
            _avisoModbus.Dock = DockStyle.Top;
            _avisoModbus.Padding = new Padding(2, 4, 0, 0);
            topo.Controls.Add(_avisoModbus);
            topo.Controls.Add(_resumoModbus);

            var cartao = new Cartao { Dock = DockStyle.Fill, Padding = new Padding(16, 14, 16, 12) };
            _gridModbus = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            Tema.Grade(_gridModbus);
            _gridModbus.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            foreach (var c in new[] { new[] { "Registro", "REGISTRO", "60" }, new[] { "Bit", "BIT", "30" }, new[] { "Faixa", "FAIXA", "55" },
                new[] { "Membro", "MEMBRO NO DB", "150" }, new[] { "Tag", "TAG DO PROGRAMA", "170" }, new[] { "Descricao", "DESCRIÇÃO", "200" } })
                _gridModbus.Columns.Add(new DataGridViewTextBoxColumn { Name = c[0], HeaderText = c[1], FillWeight = int.Parse(c[2]), SortMode = DataGridViewColumnSortMode.NotSortable });
            cartao.Controls.Add(_gridModbus);

            var espaco = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Tema.Fundo };
            host.Controls.Add(cartao);
            host.Controls.Add(espaco);
            host.Controls.Add(topo);

            return NovaPagina("Mapa Modbus", "CLP servidor · DB COM · holding registers",
                "Montado sozinho, na ordem das listas: comandos binários (CMD_AUT, CMD_LIG/ABR), set de velocidade, status, velocidades, "
                + "níveis, defeitos, status words e DWords. Itens novos no fim da lista não mudam os registros dos que já existem.",
                new Control[]
                {
                    Tema.Botao("Gerar comunicação", true, delegate { AbrirGerar(Escopo.Modbus); }, "gerar"),
                    Tema.Botao("Exportar mapa (CSV)", false, delegate { ExportarMapa(); }, "exportar")
                }, host);
        }

        /// <summary>Words DEFEITOS_n: as que estao na página Tags, ou uma estimativa pelo numero de defeitos.</summary>
        private int EstimarDefeitos()
        {
            string pref = string.IsNullOrWhiteSpace(Cfg.NomeWordDefeitos) ? "DEFEITOS_" : Cfg.NomeWordDefeitos.Trim();
            int naAba = _ios.Count(i => (i.Nome ?? "").StartsWith(pref, StringComparison.OrdinalIgnoreCase));
            int bits = 1;   // emergencia
            foreach (Partida p in _partidas)
            {
                bits++;
                if (!Vazio(p.TagProtecao)) bits++;
                if (Gerador.TemExaustor(p)) bits++;
            }
            foreach (Partida p in _sensores)
            {
                Gerador.TipoPartida t = Gerador.BuscarTipo(p.Tipo);
                if (t != null && t.SenRaspador) bits += 2;
            }
            return Math.Max(naAba, (bits + 15) / 16);
        }

        private MapaModbus MapaAtual()
        {
            LerConfigDaTela();
            var todas = _partidas.Concat(_sensores).Concat(_pids).Where(x => !PartidaVazia(x)).ToList();
            return MapaModbus.Montar(todas, EstimarDefeitos());
        }

        private void AtualizarModbus()
        {
            if (_gridModbus == null) return;
            MapaModbus m = MapaAtual();

            _resumoModbus.Controls.Clear();
            Action<string, int, int, int, int, string> faixa = delegate (string nome, int ini, int qtd, int usado, int cap, string un)
            {
                bool estourou = qtd > cap;
                var c = new Cartao { Width = 258, Height = 82, Padding = new Padding(14, 10, 10, 8), Margin = new Padding(0, 0, 12, 0), BarraAzul = true };
                if (estourou) c.CorBorda = Tema.Amarelo;
                var t = new Label { Text = nome, AutoSize = true, Font = Tema.Fonte(10f, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent, Location = new Point(16, 9) };
                var r = new Label { Text = ini + " – " + (ini + qtd - 1), AutoSize = true, Font = Tema.Mono, ForeColor = estourou ? Tema.Amarelo : Tema.Ciano, BackColor = Color.Transparent, Location = new Point(16, 33) };
                var u = new Label { Text = usado + " de " + qtd + " " + un + (estourou ? "  (padrão " + cap + ")" : ""), AutoSize = true, Font = Tema.Pequena, ForeColor = Tema.TextoSec, BackColor = Color.Transparent, Location = new Point(16, 56) };
                c.Controls.Add(t); c.Controls.Add(r); c.Controls.Add(u);
                _resumoModbus.Controls.Add(c);
            };
            faixa("Comandos binários", m.IniCmd, m.QtdCmd, m.Comandos.Count, MapaModbus.PadraoCmd, "words  ·  " + m.Comandos.Count + "/" + m.QtdCmd * 16 + " bits");
            faixa("Escrita (words)", m.IniEsc, m.QtdEsc, m.Escritas.Count, MapaModbus.PadraoEsc, "usados");
            faixa("Leitura (words)", m.IniLei, m.QtdLei, m.Leituras.Count, MapaModbus.PadraoLei, "usados");
            faixa("Leitura (DWords)", m.IniDw, m.QtdDw, m.DWords.Count * 2, MapaModbus.PadraoDw, "registros");

            _avisoModbus.Text = (m.ForaDoPadrao
                ? "⚠ Não coube no padrão 40001–40110: as faixas cresceram e o mapa vai até " + (MapaModbus.Base + m.TotalRegistros - 1) + ". "
                : "✓ Dentro do padrão (40001–40110). ")
                + "MB_HOLD_REG = P#DB" + Cfg.ComNumero + ".DBX0.0 BYTE " + m.TotalBytes + ".";
            _avisoModbus.ForeColor = m.ForaDoPadrao ? Tema.Amarelo : Tema.Verde;

            _gridModbus.SuspendLayout();
            _gridModbus.Rows.Clear();
            foreach (ItemModbus i in m.Itens())
            {
                int k = _gridModbus.Rows.Add(i.Registro, i.Bit >= 0 ? i.Bit.ToString() : "", i.Faixa, i.Membro, i.Tag, i.Descricao);
                if (i.Descricao == "RESERVA") _gridModbus.Rows[k].DefaultCellStyle.ForeColor = Tema.TextoFraco;
            }
            _gridModbus.ResumeLayout();
        }

        private void ExportarMapa()
        {
            MapaModbus m = MapaAtual();
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = "Planilha CSV (*.csv)|*.csv";
                dlg.FileName = "mapa_modbus" + (_arquivoAtual != null ? "_" + Path.GetFileNameWithoutExtension(_arquivoAtual) : "") + ".csv";
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    File.WriteAllText(dlg.FileName, m.Csv(), new System.Text.UTF8Encoding(true));   // BOM: acentos certos no Excel
                    Log("Mapa Modbus exportado: " + dlg.FileName + " (" + m.TotalRegistros + " registros).");
                }
                catch (Exception ex)
                {
                    Dialogo.Erro(this, "Não foi possível salvar:\n" + ex.Message);
                }
            }
        }

        // ===================================================================
        //  Maquinas
        // ===================================================================
        private Control CriarPaginaMaquinas()
        {
            var host = new Panel { BackColor = Tema.Fundo, AutoScroll = true };
            Win.Escurecer(host);
            var fluxo = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Tema.Fundo };

            fluxo.Controls.Add(Tema.Secao("Catálogo — clique para adicionar"));
            var catalogo = new FlowLayoutPanel { AutoSize = true, WrapContents = true, BackColor = Tema.Fundo, Margin = new Padding(0, 4, 0, 18) };

            // grupo 1: máquinas
            if (ReceitaRegistry.Maquinas.Count > 0)
            {
                foreach (IReceitaMaquina r in ReceitaRegistry.Maquinas)
                {
                    IReceitaMaquina receita = r;
                    var cartao = new CartaoInicio(receita.NomeExibicao, receita.Descricao, receita.Icone) { Situacao = "Máquina · adicionar ao projeto", Height = 186 };
                    cartao.Click += delegate { NovaReceita(receita); };
                    catalogo.Controls.Add(cartao);
                }
            }

            // grupo 2: subsistemas
            if (ReceitaRegistry.Subsistemas.Count > 0)
            {
                foreach (IReceitaSubsistema r in ReceitaRegistry.Subsistemas)
                {
                    IReceitaSubsistema receita = r;
                    var cartao = new CartaoInicio(receita.NomeExibicao, receita.Descricao, receita.Icone) { Situacao = "Subsistema · adicionar ao projeto", Height = 186 };
                    cartao.Click += delegate { NovaSubsistema(receita); };
                    catalogo.Controls.Add(cartao);
                }
            }

            var cMais = new CartaoInicio("Outras máquinas", "Mande o PDF da próxima máquina do escopo que ela entra no catálogo do mesmo jeito.", "mais") { Situacao = "Em breve", CorSituacao = Tema.TextoFraco, Height = 186, Clicavel = false };
            cMais.Cursor = Cursors.Default;
            catalogo.Controls.Add(cMais);
            fluxo.Controls.Add(catalogo);

            fluxo.Controls.Add(Tema.Secao("Máquinas neste projeto"));
            _listaMaquinas = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Tema.Fundo, Margin = new Padding(0, 4, 0, 0) };
            fluxo.Controls.Add(_listaMaquinas);
            host.Controls.Add(fluxo);

            return NovaPagina("Máquinas", "Escopos completos · partidas e sensores que trabalham juntos",
                "Escolha uma máquina do catálogo: ela cria de uma vez todas as partidas e sensores com a nomenclatura padrão, "
                + "e na geração entram também o bloco da máquina e o automático. Depois é só preencher as tags nas páginas Partidas e Sensores.",
                new Control[] { Tema.Botao("Configurar padrões", false, delegate { using (var f = new CatalogoMaquinasForm()) if (f.ShowDialog(this) == DialogResult.OK) Log("Catálogo de máquinas atualizado. Novos flotadores usarão os padrões salvos."); }, "config") }, host);
        }

        private void AtualizarMaquinas()
        {
            if (_listaMaquinas == null) return;
            _listaMaquinas.Controls.Clear();
            var todas = _partidas.Concat(_sensores).Concat(_pids).ToList();
            List<string> maqs = Maquinas.MaquinasNoProjeto(todas);
            if (maqs.Count == 0)
            {
                _listaMaquinas.Controls.Add(Tema.Ajuda("Nenhuma máquina ainda. Clique num cartão do catálogo acima para adicionar."));
                return;
            }
            foreach (string mq in maqs)
            {
                string id = mq;
                var itens = todas.Where(x => string.Equals(x.Maquina, id, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Subsistema, id, StringComparison.OrdinalIgnoreCase)).ToList();
                Partida sis = itens.FirstOrDefault(x => x.Papel == Maquinas.PapelSis);
                int pend = itens.Count(x => Resumo(Gerador.EhSensor(x) ? _cadSen : Gerador.EhPid(x) ? _cadPid : _cadPart, x).StartsWith("⚠"));

                string titulo = sis != null ? Regex.Replace(sis.Descricao ?? "", @"\s*-\s*AUTOM[ÁA]TICO\s*$", "") : "";
                var c = new Cartao { Width = 1022, Padding = new Padding(18, 12, 14, 10), Margin = new Padding(0, 0, 0, 10), BarraAzul = true };
                var t = new Label { Text = id + "  ·  " + titulo, AutoSize = true, Font = Tema.Fonte(11.5f, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.Transparent, Location = new Point(20, 11), UseMnemonic = false };
                var d = new Label
                {
                    Text = string.Join("   ", itens.Select(x => x.Sub + "." + x.Eq + "." + x.Fn).ToArray()),
                    Font = Tema.Pequena, ForeColor = Tema.TextoSec, BackColor = Color.Transparent, Location = new Point(21, 39), UseMnemonic = false
                };
                Tema.Medir(d, c.Width - 470);
                var pdl = new Label
                {
                    Text = pend == 0 ? "✓ Tudo preenchido" : "⚠ " + Plural(pend, "item com campos a preencher", "itens com campos a preencher") + " — clique em Editar",
                    AutoSize = true, Font = Tema.Fonte(8.5f, FontStyle.Bold), ForeColor = pend == 0 ? Tema.Verde : Tema.Amarelo, BackColor = Color.Transparent, Location = new Point(21, d.Bottom + 6)
                };
                c.Height = pdl.Bottom + 26;
                
                string receitaSub = itens.FirstOrDefault(x => !string.IsNullOrEmpty(x.Subsistema) && x.Subsistema == id)?.Receita;
                string receitaNome = receitaSub ?? itens.Select(x => x.Receita).FirstOrDefault(r => !string.IsNullOrEmpty(r));
                string receita = receitaNome ?? Maquinas.Preparador;
                if (sis == null) titulo = receita;
                t.Text = id + "  ·  " + titulo;
                
                var edi = Tema.Botao("Editar", true, delegate { EditarMaquina(id); });
                edi.AutoSize = false;
                edi.Size = new Size(110, 38);
                edi.Location = new Point(c.Width - 292, 22);
                c.Controls.Add(edi);
                
                var receitaObj = ReceitaRegistry.PorNome(receitaNome);
                if (receitaObj != null)
                {
                    var rec = Tema.Botao("Reconfigurar", false, delegate { ReconfigurarReceita(id); });
                    rec.AutoSize = false;
                    rec.Size = new Size(130, 38);
                    rec.Location = new Point(c.Width - 432, 22);
                    c.Controls.Add(rec);
                }
                
                var rem = (BotaoTema)Tema.Botao("Remover", false, delegate { RemoverMaquina(id); }, "lixeira");
                rem.Perigo = true;
                rem.AutoSize = false;
                rem.Size = new Size(150, 38);
                rem.Location = new Point(c.Width - 170, 22);
                c.Controls.Add(t); c.Controls.Add(d); c.Controls.Add(pdl); c.Controls.Add(rem);
                _listaMaquinas.Controls.Add(c);
            }
        }

        private void ReconfigurarReceita(string id)
        {
            // Procura os itens da máquina E os itens do subsistema (para achar periféricos)
            var itens = _partidas.Concat(_sensores).Concat(_pids)
                .Where(x => string.Equals(x.Maquina, id, StringComparison.OrdinalIgnoreCase) || 
                            string.Equals(x.Subsistema, id, StringComparison.OrdinalIgnoreCase)).ToList();
            
            if (itens.Count == 0) return;
            
            // Tenta achar a receita: primeiro olha se é um subsistema, senão pega a receita da máquina
            string receitaSub = itens.FirstOrDefault(x => !string.IsNullOrEmpty(x.Subsistema) && string.Equals(x.Subsistema, id, StringComparison.OrdinalIgnoreCase))?.Receita;
            string receitaNome = receitaSub ?? itens.Select(x => x.Receita).FirstOrDefault(r => !string.IsNullOrEmpty(r));
            
            IReceitaMaquina receita = ReceitaRegistry.PorNome(receitaNome) ?? ReceitaRegistry.PorId(receitaNome);
            if (receita == null)
            {
                Dialogo.Aviso(this, "A máquina " + id + " foi criada com uma receita que não está mais registrada ("
                    + (receitaNome ?? "?") + "). Não é possível reconfigurá-la automaticamente.");
                return;
            }

            ContextoReceita ctx = ContextoAtual();
            object opcoesAtuais = receita.OpcoesDe(ctx, itens, id);
            
            using (Form form = receita.CriarForm(id, ctx, ctx.IdEmUso, opcoesAtuais))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                var resultado = form as IRetornaResultadoReceita;
                if (resultado == null || resultado.Resultado == null) return;

                PlanoReconfiguracao plano = receita.Planejar(ctx, id, itens, resultado.Resultado.Opcoes);

                var listas = new ListasDoProjeto { Partidas = _partidas, Sensores = _sensores, Pids = _pids };
                Func<List<Partida>, bool> confirmar = delegate (List<Partida> removidas)
                {
                    string lista = string.Join(", ", removidas.Select(x => x.Sub + "." + x.Eq + "." + x.Fn).ToArray());
                    return _confirmar("Estes itens deixam de existir e serão apagados (com as tags que já preencheu neles):\n\n"
                        + lista + "\n\nContinuar?");
                };

                _carregando = true;
                ResultadoAplicacao res;
                try
                {
                    res = AplicadorPlano.Aplicar(receita, plano, confirmar, listas, _ios, PreencherAutomaticos);
                }
                finally
                {
                    _carregando = false;
                }
                if (res.CanceladoPeloUsuario) return;

                if (_maqAtual == plano.IdAntigo && plano.Renomeia) _maqAtual = plano.IdNovo;
                ReligarTags();
                foreach (Cadastro c in new[] { _cadPart, _cadSen, _cadPid }) MontarEditor(c, true);
                AoMudarDados();
                AtualizarMaquinas();
                Log("Máquina/Subsistema " + plano.IdNovo + " reconfigurado: " + res.Resumo());
                if (plano.Renomeia)
                    Log("AVISO: o DB " + plano.IdAntigo + " que já foi gerado no TIA continua lá; apague-o (e a FC antiga) se não for mais usar. Gere a máquina de novo para criar o " + plano.IdNovo + ".");
            }
        }

        private void NovaMaquina()
        {
            var todas = _partidas.Concat(_sensores).ToList();
            int n = 1;
            while (todas.Any(x => string.Equals(x.Sub, "PP" + n, StringComparison.OrdinalIgnoreCase) || x.Maquina == "PP" + n)) n++;
            using (var dlg = new MaquinaForm("PP" + n, db => todas.Any(x => string.Equals(x.Sub, db, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.Maquina, db, StringComparison.OrdinalIgnoreCase) || (x.Sub == "SEN" && string.Equals(x.Eq, db, StringComparison.OrdinalIgnoreCase)))))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                foreach (Partida p in dlg.Linhas)
                {
                    PreencherAutomaticos(p);
                    if (Gerador.EhSensor(p)) _sensores.Add(p); else _partidas.Add(p);
                }
                AoMudarDados();
                AtualizarMaquinas();
                Log("Máquina " + dlg.Linhas[0].Maquina + " criada: " + dlg.Linhas.Count + " itens. Preencha as tags nas páginas Partidas e Sensores.");
            }
        }

        // ===================================================================
        //  Pagina de uma maquina / subsistema
        // ===================================================================
        private Cadastro CadDe(Partida p)
        {
            return Gerador.EhSensor(p) ? _cadSen : Gerador.EhPid(p) ? _cadPid : _cadPart;
        }

        private List<Secao> SecoesDoItem(Partida p)
        {
            return Gerador.EhSensor(p) ? SecoesSensor(p) : Gerador.EhPid(p) ? SecoesPid(p) : SecoesPartida(p);
        }

        private Control CriarPaginaMaquina()
        {
            _cadMaq = new Cadastro { Nome = "item", Lista = _itensMaq, Secoes = SecoesDoItem, Novo = delegate { return new Partida(); } };
            var colCat = Col("Categoria", "CATEGORIA", 62);
            colCat.ReadOnly = true;
            var colTipo = Col("Tipo", "TIPO", 170);
            colTipo.ReadOnly = true;
            Control conteudo = CriarCadastro(_cadMaq, new DataGridViewColumn[]
            {
                colCat, Col("Sub", "DB", 56), Col("Eq", "GRUPO", 60), Col("Fn", "NOME", 56), colTipo, Col("Descricao", "DESCRIÇÃO", 160)
            });

            // faixa com o nome da maquina
            var faixa = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Tema.Fundo };
            _maqTitulo = new Label { AutoSize = true, Font = Tema.Fonte(13.5f, FontStyle.Bold), ForeColor = Color.White, Location = new Point(0, 0), UseMnemonic = false };
            _maqResumo = new Label { AutoSize = true, Font = Tema.Fonte(8.75f, FontStyle.Bold), ForeColor = Tema.TextoSec, Location = new Point(2, 30), UseMnemonic = false };
            faixa.Controls.Add(_maqTitulo);
            faixa.Controls.Add(_maqResumo);
            var host = new Panel { BackColor = Tema.Fundo };
            conteudo.Dock = DockStyle.Fill;
            host.Controls.Add(conteudo);
            host.Controls.Add(faixa);

            var novo = Tema.Botao("Novo item", false, null, "mais");
            var menu = MenuEscuro.Novo();
            menu.Items.Add("Partida", null, delegate { NovoItemMaquina("Partida"); });
            menu.Items.Add("Sensor", null, delegate { NovoItemMaquina("Sensor"); });
            menu.Items.Add("PID", null, delegate { NovoItemMaquina("PID"); });
            var cadastrados = CatalogoMaquinas.Dados.Itens.Where(x => x.Ativo && x.Categoria == "Equipamento").ToList();
            if (cadastrados.Count > 0)
            {
                menu.Items.Add(new ToolStripSeparator());
                foreach (DefinicaoComponente item in cadastrados)
                {
                    DefinicaoComponente copia = item;
                    menu.Items.Add("Catálogo: " + copia.Nome + " (" + copia.Codigo + ")", null, delegate { NovoEquipamentoCatalogo(copia); });
                }
            }
            novo.Click += delegate { menu.Show(novo, new Point(0, novo.Height)); };

            // dentro da maquina nao ha filtro nem caixinha GERAR (o botao gera a maquina toda)
            Control quadroFiltro = _cadMaq.Filtro.Parent;                    // a moldura do filtro
            var cabBusca = quadroFiltro.Parent as TableLayoutPanel;
            if (cabBusca != null)
            {
                if (cabBusca.GetControlFromPosition(1, 0) != null) cabBusca.GetControlFromPosition(1, 0).Visible = false;
                quadroFiltro.Visible = false;
                cabBusca.ColumnStyles[1].Width = 0;
            }
            foreach (DataGridViewColumn c in _cadMaq.Grade.Columns)
                if (c.DataPropertyName == "Marcada") c.Visible = false;

            return NovaPagina("Máquina", "Todos os itens da máquina num lugar só", null,
                new Control[]
                {
                    Tema.Botao("Gerar esta máquina", true, delegate { GerarMaquina(); }, "gerar"),
                    novo,
                    Tema.Botao("Trazer avulsos", false, delegate { AdicionarAvulsos(); }, "lista"),
                    Tema.Botao("Tirar da máquina", false, delegate { TirarDaMaquina(); }, "soltar")
                }, host, "Máquinas", delegate { MostrarPagina(_pagMaquinas); });
        }

        private List<Partida> ItensDaMaquina(string id)
        {
            return _partidas.Where(x => string.Equals(x.Maquina, id, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Subsistema, id, StringComparison.OrdinalIgnoreCase))
                .Concat(_sensores.Where(x => string.Equals(x.Maquina, id, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Subsistema, id, StringComparison.OrdinalIgnoreCase)))
                .Concat(_pids.Where(x => string.Equals(x.Maquina, id, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Subsistema, id, StringComparison.OrdinalIgnoreCase))).ToList();
        }

        private void AbrirMaquina(string id)
        {
            _maqAtual = id;
            CarregarItensMaquina();
            MostrarPagina(_pagMaquina);
            if (_itensMaq.Count > 0) Selecionar(_cadMaq, _itensMaq[0]);
            MontarEditor(_cadMaq, true);
        }

        private void CarregarItensMaquina()
        {
            List<Partida> itens = ItensDaMaquina(_maqAtual);
            _carregando = true;
            try
            {
                _itensMaq.RaiseListChangedEvents = false;
                _itensMaq.Clear();
                foreach (Partida p in itens) _itensMaq.Add(p);
            }
            finally
            {
                _itensMaq.RaiseListChangedEvents = true;
                _carregando = false;
            }
            _itensMaq.ResetBindings();
            AplicarFiltro(_cadMaq);

            string receita = itens.Select(x => x.Receita).FirstOrDefault(r => !string.IsNullOrEmpty(r))
                ?? (itens.Any(x => Maquinas.EhPreparador(x)) ? Maquinas.Preparador : "Máquina");
            Partida sis = itens.FirstOrDefault(x => x.Papel == Maquinas.PapelSis);
            string desc = sis != null ? Regex.Replace(sis.Descricao ?? "", @"\s*-\s*AUTOM[ÁA]TICO\s*$", "") : receita;
            _maqTitulo.Text = _maqAtual + "  ·  " + desc;
            int pend = itens.Count(x => Resumo(CadDe(x), x).StartsWith("⚠"));
            _maqResumo.Text = receita + "   ·   " + Plural(itens.Count(x => x.Categoria == "Partida"), "partida", "partidas") + ", "
                + Plural(itens.Count(x => x.Categoria == "Sensor"), "sensor", "sensores") + ", " + Plural(itens.Count(x => x.Categoria == "PID"), "PID", "PIDs")
                + (pend > 0 ? "   ·   ⚠ " + pend + " com campos a preencher" : "   ·   ✓ tudo preenchido");
            _maqResumo.ForeColor = pend > 0 ? Tema.Amarelo : Tema.Verde;
        }
        
        private void NovaSubsistema(IReceitaSubsistema receita)
        {
            NovaReceita(receita);   // IReceitaSubsistema é IReceitaMaquina
        }

                

    


        private void NovoItemMaquina(string categoria)
        {
            if (_maqAtual == null) return;
            FinalizarEdicao();
            var itens = ItensDaMaquina(_maqAtual);
            string subPadrao = itens.Where(x => x.Categoria == "Partida").GroupBy(x => x.Sub).OrderByDescending(g => g.Count())
                .Select(g => g.Key).FirstOrDefault() ?? _maqAtual;
            Partida p;
            if (categoria == "Sensor")
            {
                p = new Partida { Sub = "SEN", Eq = _maqAtual, Tipo = Gerador.NomesTiposSensor[0], Maquina = _maqAtual };
                _sensores.Add(p);
            }
            else if (categoria == "PID")
            {
                p = new Partida { Sub = subPadrao, Eq = "", Fn = "PID", Tipo = "PID", Maquina = _maqAtual };
                _pids.Add(p);
            }
            else
            {
                p = new Partida { Sub = subPadrao, Eq = "", Tipo = Gerador.NomesTiposPartida[0], Auto = true, Maquina = _maqAtual };
                _partidas.Add(p);
            }
            CarregarItensMaquina();
            Selecionar(_cadMaq, p);
            MontarEditor(_cadMaq, true, categoria == "Sensor" ? "Fn" : "Eq");
            AoMudarDados();
            Log(categoria + " nova na máquina " + _maqAtual + ": preencha o nome e os campos à direita.");
        }

        private void NovoEquipamentoCatalogo(DefinicaoComponente definicao)
        {
            if (_maqAtual == null || definicao == null) return;
            FinalizarEdicao();
            var itens = ItensDaMaquina(_maqAtual);
            string subPadrao = itens.Where(x => x.Categoria == "Partida").GroupBy(x => x.Sub).OrderByDescending(g => g.Count())
                .Select(g => g.Key).FirstOrDefault() ?? _maqAtual;
            string eq = (definicao.Codigo ?? "EQ").Trim().ToUpperInvariant();
            string fn = "BBA";
            int n = 1;
            while (itens.Any(x => x.Sub == subPadrao && x.Eq == eq && x.Fn == fn)) { n++; fn = "BB" + n; }
            string descricao = (definicao.DescricaoPadrao ?? "").Replace("{DESC}", _maqAtual).Replace("{NOME}", definicao.Nome ?? "");
            var p = new Partida { Sub = subPadrao, Eq = eq, Fn = fn, Tipo = string.IsNullOrWhiteSpace(definicao.TipoPadrao) ? Gerador.NomesTiposPartida[0] : definicao.TipoPadrao, Descricao = descricao, Auto = true, Maquina = _maqAtual };
            _partidas.Add(p);
            CarregarItensMaquina(); Selecionar(_cadMaq, p); MontarEditor(_cadMaq, true);
            AoMudarDados();
            Log("Equipamento " + eq + "." + fn + " criado a partir do catálogo. Confira o tipo e complete os sinais.");
        }

        private void AdicionarAvulsos()
        {
            if (_maqAtual == null) return;
            FinalizarEdicao();
            var avulsos = _partidas.Concat(_sensores).Concat(_pids).Where(x => string.IsNullOrEmpty(x.Maquina) && !PartidaVazia(x)).ToList();
            if (avulsos.Count == 0)
            {
                Dialogo.Info(this, "Não há itens avulsos (fora de máquina) no projeto.");
                return;
            }
            using (var f = new Form
            {
                Text = "Trazer itens avulsos para a máquina " + _maqAtual, Size = new Size(720, 560), StartPosition = FormStartPosition.CenterParent,
                MinimizeBox = false, ShowInTaskbar = false
            })
            {
                Tema.Janela(f);
                var lista = new CheckedListBox { Dock = DockStyle.Fill, BackColor = Tema.Cartao, ForeColor = Tema.Texto, BorderStyle = BorderStyle.None, CheckOnClick = true, Font = Tema.Normal };
                foreach (Partida p in avulsos)
                    lista.Items.Add(p.Categoria.ToUpperInvariant() + "   " + p.Sub + "." + p.Eq + "." + p.Fn + "   —   " + (p.Descricao ?? "") + "   (" + p.Tipo + ")");
                var dica = Tema.Ajuda("Marque os itens que fazem parte desta máquina. Eles continuam gerando como antes; só passam a ser editados e gerados junto com ela.", 660);
                dica.Dock = DockStyle.Top;
                var botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(10, 12, 2, 0), BackColor = Tema.FundoEscuro };
                var ok = Tema.Botao("Trazer para a máquina", true, delegate { f.DialogResult = DialogResult.OK; });
                var cancela = Tema.Botao("Cancelar", false, delegate { f.DialogResult = DialogResult.Cancel; });
                botoes.Controls.Add(ok);
                botoes.Controls.Add(cancela);
                var miolo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
                miolo.Controls.Add(lista);
                miolo.Controls.Add(dica);
                f.Controls.Add(miolo);
                f.Controls.Add(botoes);
                if (f.ShowDialog(this) != DialogResult.OK) return;
                int n = 0;
                foreach (int i in lista.CheckedIndices) { avulsos[i].Maquina = _maqAtual; n++; }
                if (n == 0) return;
                CarregarItensMaquina();
                AoMudarDados();
                Log(n + " item(ns) trazido(s) para a máquina " + _maqAtual + ".");
            }
        }

        private void TirarDaMaquina()
        {
            FinalizarEdicao();
            var sel = _cadMaq.Grade.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Index)
                .Where(i => i >= 0 && i < _itensMaq.Count).Select(i => _itensMaq[i]).ToList();
            if (sel.Count == 0 && _cadMaq.Editando != null) sel.Add(_cadMaq.Editando);
            if (sel.Count == 0) return;
            var presos = sel.Where(Maquinas.EhPreparador).ToList();   // pecas do preparador: dependem da maquina
            var soltar = sel.Except(presos).ToList();
            if (presos.Count > 0)
                Dialogo.Info(this, presos.Count + " item(ns) fazem parte da lógica do preparador e não podem sair dele (use Remover máquina).");
            if (soltar.Count == 0) return;
            if (!_confirmar("Tirar " + soltar.Count + " item(ns) da máquina " + _maqAtual + "? Eles continuam no projeto, como avulsos.")) return;
            foreach (Partida p in soltar) p.Maquina = "";
            CarregarItensMaquina();
            MontarEditor(_cadMaq, true);
            AoMudarDados();
        }

        private void GerarMaquina()
        {
            if (_maqAtual == null) return;
            FinalizarEdicao();
            var itens = ItensDaMaquina(_maqAtual).Where(x => !PartidaVazia(x)).ToList();
            if (itens.Count == 0) return;
            AbrirGerar(Escopo.Maquina);
        }

        private void NovoFlotador()
        {
            var todas = _partidas.Concat(_sensores).Concat(_pids).ToList();
            int n = 1;
            while (todas.Any(x => string.Equals(x.Sub, "FL" + n, StringComparison.OrdinalIgnoreCase) || x.Maquina == "FL" + n)) n++;
            var preparadores = todas.Where(x => Maquinas.EhPreparador(x)).Select(x => x.Maquina).Distinct().ToList();
            using (var dlg = new FlotadorForm("FL" + n, preparadores,
                db => todas.Any(x => string.Equals(x.Sub, db, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Maquina, db, StringComparison.OrdinalIgnoreCase))))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                List<Partida> linhas = Maquinas.CriarFlotador(dlg.Opcoes, todas);
                foreach (Partida p in linhas)
                {
                    PreencherAutomaticos(p);
                    if (Gerador.EhSensor(p)) _sensores.Add(p);
                    else if (Gerador.EhPid(p)) _pids.Add(p);
                    else _partidas.Add(p);
                }
                AoMudarDados();
                AtualizarMaquinas();
                Log("Flotador " + dlg.Opcoes.Sub + " criado: " + linhas.Count + " itens, com intertravamentos e modulações. "
                    + "Clique em Editar na máquina para preencher as tags.");
            }
        }

        /// <summary>Muda a linha (ex.: o nome da bomba) e leva junto as tags que ainda tinham o nome automatico antigo (a tag da aba Tags tambem e renomeada).</summary>
        private void SeguirNomeAutomatico(Partida p, Action mudar)
        {
            Func<Dictionary<string, string>> nomes = delegate
            {
                var d = new Dictionary<string, string>(NomesAutomaticosIo(p));
                d["TagDef"] = AutoDef(p);
                d["TagProtecaoMem"] = AutoPrt(p);
                return d;
            };
            Dictionary<string, string> antes = nomes();
            mudar();
            foreach (var par in nomes())
            {
                PropertyInfo pi = typeof(Partida).GetProperty(par.Key);
                if (pi == null) continue;
                string atual = Convert.ToString(pi.GetValue(p, null) ?? "");
                string ant;
                if (string.IsNullOrEmpty(atual) || !antes.TryGetValue(par.Key, out ant) || atual != ant || ant == par.Value) continue;
                Io io = _ios.FirstOrDefault(x => string.Equals(x.Nome, atual, StringComparison.OrdinalIgnoreCase));
                if (io != null && !_ios.Any(x => string.Equals(x.Nome, par.Value, StringComparison.OrdinalIgnoreCase))) io.Nome = par.Value;
                pi.SetValue(p, par.Value, null);
            }
        }

        private void RemoverMaquina(string id)
        {
            // Procura tanto os itens da máquina quanto os periféricos do subsistema
            var itens = _partidas.Concat(_sensores).Concat(_pids)
                .Where(x => string.Equals(x.Maquina, id, StringComparison.OrdinalIgnoreCase) || 
                            string.Equals(x.Subsistema, id, StringComparison.OrdinalIgnoreCase)).ToList();
            
            if (!_confirmar("Remover a máquina/subsistema " + id + " e seus " + itens.Count + " itens (partidas, sensores e PIDs)?")) return;
            
            _carregando = true;
            try
            {
                foreach (Partida p in itens) 
                { 
                    _partidas.Remove(p); 
                    _sensores.Remove(p); 
                    _pids.Remove(p); 
                }
            }
            finally
            {
                _carregando = false;
            }
            foreach (Cadastro c in new[] { _cadPart, _cadSen, _cadPid }) MontarEditor(c, true);
            AoMudarDados();
            AtualizarMaquinas();
            Log("Máquina/Subsistema " + id + " removido.");
        }

        // ===================================================================
        //  Receitas: criação e reconfiguração genérica
        //  (substitui NovaMaquina / NovoFlotador / ReconfigurarPreparador / ReconfigurarFlotador)
        // ===================================================================
        private ContextoReceita ContextoAtual()
        {
            var todas = _partidas.Concat(_sensores).Concat(_pids).Cast<Partida>().ToList();
            return new ContextoReceita(todas, _projetoHardware);
        }

        private void NovaReceita(IReceitaMaquina receita)
        {
            if (receita == null) return;

            object opcoesIniciais = receita.OpcoesPadrao;
            List<PerfilReceita> perfis = PerfisReceita.Listar(receita.Id);
            if (perfis.Count > 0)
            {
                using (var pf = new PerfilForm(receita.NomeExibicao, perfis))
                {
                    if (pf.ShowDialog(this) != DialogResult.OK) return;
                    if (pf.Escolhido != null)
                    {
                        try
                        {
                            Type tipo = receita.OpcoesPadrao.GetType();
                            opcoesIniciais = PerfisReceita.Desserializar(pf.Escolhido.OpcoesXml, tipo) ?? receita.OpcoesPadrao;
                        }
                        catch { opcoesIniciais = receita.OpcoesPadrao; }
                    }
                }
            }

            ContextoReceita ctx = ContextoAtual();
            string sugestao = receita.SugerirId(ctx);
            using (Form form = receita.CriarForm(sugestao, ctx, ctx.IdEmUso, opcoesIniciais))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
                var resultado = form as IRetornaResultadoReceita;
                if (resultado == null || resultado.Resultado == null) return;

                List<Partida> novas = receita.Criar(ctx, resultado.Resultado.Opcoes);
                _carregando = true;
                try
                {
                    foreach (Partida p in novas)
                    {
                        PreencherAutomaticos(p);
                        if (Gerador.EhSensor(p)) _sensores.Add(p);
                        else if (Gerador.EhPid(p)) _pids.Add(p);
                        else _partidas.Add(p);
                    }
                }
                finally { _carregando = false; }
                AoMudarDados();
                AtualizarMaquinas();
                if (novas.Count > 0)
                    Log("Máquina " + novas[0].Maquina + " criada: " + novas.Count + " itens. Preencha as tags nas páginas Partidas e Sensores.");
            }
        }


        /// <summary>Tags criadas pelo gerador (defeitos): entram na página Tags e ficam salvas no projeto.</summary>
        private void ReceberTags(List<Io> novas)
        {
            _carregando = true;
            try
            {
                foreach (Io io in novas)
                    if (!_ios.Any(x => string.Equals(x.Nome, io.Nome, StringComparison.OrdinalIgnoreCase))) _ios.Add(io);
            }
            finally
            {
                _carregando = false;
            }
            ReligarTags();   // inclui as que ja estavam na aba e so ganharam endereco
            AoMudarDados();
        }

        private void Habilitar(bool ligado)
        {
            foreach (Button b in _botoesTopo) b.Enabled = ligado;
            foreach (ItemNav n in new[] { _navInicio, _navTags, _navMaq, _navPart, _navSen, _navPid, _navModbus, _navGerar }) n.Enabled = ligado;
            _hostPaginas.Enabled = ligado;
            Cursor = ligado ? Cursors.Default : Cursors.AppStarting;
        }

        // ===================================================================
        //  Utilitarios
        // ===================================================================
        private static bool Vazio(string s)
        {
            return string.IsNullOrWhiteSpace(s);
        }

        private static bool PartidaVazia(Partida p)
        {
            return Vazio(p.Sub) && Vazio(p.Eq) && Vazio(p.Fn) && Vazio(p.Descricao)
                && Vazio(p.TagConf) && Vazio(p.TagRef) && Vazio(p.TagHab) && Vazio(p.TagDef)
                && Vazio(p.TagHabC1) && Vazio(p.TagHabC2) && Vazio(p.TagDefDrive)
                && Vazio(p.TagConfFec) && Vazio(p.TagFec) && Vazio(p.TagPosAi) && Vazio(p.TagSensor)
                && Vazio(p.TagPulso) && Vazio(p.TagSensor2) && Vazio(p.ModVazao) && Vazio(p.ModPid)
                && Vazio(p.TagProtecao) && Vazio(p.TagExaConf) && Vazio(p.TagExaHab);
        }

        private static bool IoVazia(Io i)
        {
            return Vazio(i.Nome) && Vazio(i.Endereco) && Vazio(i.Descricao);
        }
    }
}