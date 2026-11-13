using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Genesis.Receitas;    // <-- adicionar

namespace Genesis
{
    /// <summary>Janela "Nova maquina: preparador de polimero" (variantes e nomes).</summary>
    internal class MaquinaForm : Form, IRetornaResultadoReceita
    {
        public string ReceitaId { get; set; } = "Preparador";
        public List<Partida> Linhas = new List<Partida>();
        private readonly TextBox _db, _desc;
        private readonly ComboBox _agit, _nivel, _rosca, _tipoAg;
        private readonly CheckBox _npo;
        private readonly Label _previa;
        private readonly Func<string, bool> _dbEmUso;
        private readonly bool _editando;
        /// <summary>No modo "reconfigurar": as opcoes escolhidas (Db ja em maiusculas).</summary>
        public OpcoesPreparador Resultado;

        /// <param name="editar">null = nova maquina; com valor = reconfigurar uma maquina existente (a janela abre preenchida).</param>
        public MaquinaForm(string sugestaoDb, Func<string, bool> dbEmUso, OpcoesPreparador editar = null)
        {
            _dbEmUso = dbEmUso;
            _editando = editar != null;
            Text = _editando ? "Reconfigurar máquina: " + sugestaoDb : "Nova máquina: " + Maquinas.Preparador;
            Size = new Size(780, 700);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            Tema.Janela(this);

            var cab = new FundoDegrade { Dock = DockStyle.Top, Height = 78 };
            var t = Tema.Titulo(Maquinas.Preparador, 16f);
            t.Location = new Point(20, 10);
            var st = Tema.Subtitulo("Rosca · agitadores · válvula de alimentação · vazão · níveis");
            st.Location = new Point(23, 46);
            cab.Controls.Add(t);
            cab.Controls.Add(st);

            var card = new CartaoTabela { Dock = DockStyle.Top, Margin = new Padding(0) };
            card.ColumnStyles[0].Width = 200;
            int linha = 0;
            Action<string, Control, string> add = delegate (string rot, Control c, string ajuda)
            {
                card.Controls.Add(new Label { Text = rot, AutoSize = true, Font = Tema.Negrito, ForeColor = Tema.Texto, BackColor = Color.Transparent, Margin = new Padding(0, 9, 8, 10) }, 0, linha);
                var b = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 12) };
                b.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                Control quadro = Tema.Emoldurar(c);
                quadro.Dock = DockStyle.Fill;
                quadro.Margin = c is CheckBox ? new Padding(0, 7, 0, 0) : new Padding(0);
                b.Controls.Add(quadro);
                if (ajuda != null)
                {
                    var a = Tema.Ajuda(ajuda, 470);
                    a.ForeColor = Tema.TextoFraco;
                    a.Margin = new Padding(1, 3, 0, 0);
                    b.Controls.Add(a);
                }
                card.Controls.Add(b, 1, linha);
                linha++;
            };

            _db = Tema.CaixaTexto(); _db.Text = sugestaoDb;
            _desc = Tema.CaixaTexto(); _desc.Text = "PREPARADOR DE POLÍMERO " + new string(sugestaoDb.Where(char.IsDigit).ToArray());
            _agit = Tema.Lista(false); _agit.Items.AddRange(new object[] { "2 agitadores", "1 agitador" }); _agit.SelectedIndex = 0;
            _nivel = Tema.Lista(false); _nivel.Items.AddRange(new object[] { "Digital: nível baixo e nível alto", "Analógico (um transmissor)" }); _nivel.SelectedIndex = 0;
            _npo = Tema.Marcador("Tem nível baixo do polímero (NPO)"); _npo.Checked = true;
            _rosca = Tema.Lista(false); _rosca.Items.AddRange(Gerador.NomesTiposPartida); _rosca.SelectedItem = "Partida inversor por rede";
            _tipoAg = Tema.Lista(false); _tipoAg.Items.AddRange(Gerador.NomesTiposPartida); _tipoAg.SelectedItem = "Partida direta";
            if (_rosca.SelectedIndex < 0) _rosca.SelectedIndex = 0;
            if (_tipoAg.SelectedIndex < 0) _tipoAg.SelectedIndex = 0;
            if (_editando)
            {
                _db.Text = editar.Db;
                _desc.Text = editar.Descricao;
                _agit.SelectedIndex = editar.Agitadores > 1 ? 0 : 1;
                _nivel.SelectedIndex = editar.NivelAnalogico ? 1 : 0;
                _npo.Checked = editar.NivelPolimero;
                if (_rosca.Items.Contains(editar.TipoRosca)) _rosca.SelectedItem = editar.TipoRosca;
                if (_tipoAg.Items.Contains(editar.TipoAgitador)) _tipoAg.SelectedItem = editar.TipoAgitador;
            }

            add("DB da máquina", _db, "Também identifica a máquina. Ex.: PP1 → \"PP1\".SIS.AUT, \"PP1\".PPF.ROS e sensores \"SEN\".PP1.VAZ."
                + (_editando ? " Trocar o nome renomeia a máquina inteira (itens, referências e tags com o nome antigo). O DB antigo que já foi gerado no TIA continua lá até você apagar." : ""));
            add("Descrição", _desc, "Vai na frente da descrição de cada item.");
            add("Agitadores", _agit, null);
            add("Nível do tanque", _nivel, null);
            add("Nível do polímero", _npo, null);
            add("Tipo da rosca", _rosca, null);
            add("Tipo dos agitadores", _tipoAg, null);

            _previa = Tema.Ajuda("", 680);
            _previa.ForeColor = Tema.TextoSec;
            _previa.Margin = new Padding(0, 6, 0, 0);
            card.Controls.Add(_previa, 0, linha);
            card.SetColumnSpan(_previa, 2);

            var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 14, 16, 8), BackColor = Tema.Fundo, AutoScroll = true };
            Win.Escurecer(corpo);
            corpo.Controls.Add(card);

            var botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 12, 6, 0), BackColor = Tema.FundoEscuro };
            var ok = Tema.Botao(_editando ? "Aplicar alterações" : "Criar máquina", true, delegate { Criar(); });
            var cancela = Tema.Botao("Cancelar", false, delegate { DialogResult = DialogResult.Cancel; });
            botoes.Controls.Add(ok);
            botoes.Controls.Add(cancela);
            var btnPerfil = Tema.Botao("Salvar como perfil…", false, delegate { SalvarComoPerfil(); }, "salvar");
            botoes.Controls.Add(btnPerfil);

            Controls.Add(corpo);
            Controls.Add(botoes);
            Controls.Add(cab);
            AcceptButton = ok;
            CancelButton = cancela;

            EventHandler prev = delegate { AtualizarPrevia(); };
            _db.TextChanged += prev; _agit.SelectedIndexChanged += prev; _nivel.SelectedIndexChanged += prev; _npo.CheckedChanged += prev;
            AtualizarPrevia();
        }

        public OpcoesPreparador Opcoes()
        {
            return new OpcoesPreparador
            {
                Db = _db.Text.Trim(),
                Descricao = _desc.Text.Trim(),
                Agitadores = _agit.SelectedIndex == 0 ? 2 : 1,
                NivelAnalogico = _nivel.SelectedIndex == 1,
                NivelPolimero = _npo.Checked,
                TipoRosca = Convert.ToString(_rosca.SelectedItem),
                TipoAgitador = Convert.ToString(_tipoAg.SelectedItem)
            };
        }
        private void SalvarComoPerfil()
        {
            
            OpcoesPreparador o = Opcoes();
            using (var dlg = new SalvarPerfilForm("Preparador padrão " + o.Db, o.Descricao))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK)
                {
                    
                    return;
                }
                
                var perfil = new PerfilReceita
                {
                    ReceitaId = ReceitaId,
                    Nome = dlg.NomePerfil,
                    Descricao = dlg.DescricaoPerfil,
                    OpcoesXml = PerfisReceita.Serializar(o, typeof(OpcoesPreparador))
                };
                try
                {
                    PerfisReceita.Salvar(perfil);
                    
                    Dialogo.Info(this, "Perfil \"" + dlg.NomePerfil + "\" salvo.");
                }
                catch (Exception ex)
                {
                   
                    Dialogo.Erro(this, "Não foi possível salvar o perfil:\n" + ex.Message);
                }
            }
        }
        private void AtualizarPrevia()
        {
            if (string.IsNullOrWhiteSpace(_db.Text)) { _previa.Text = ""; return; }
            List<Partida> l = Maquinas.CriarPreparador(Opcoes());
            _previa.Text = (_editando ? "Ficará com:  " : "Vai criar:  ") + string.Join("   ·   ", l.Select(p => p.Sub + "." + p.Eq + "." + p.Fn).ToArray());
            Tema.Medir(_previa, 680);
        }

        private void Criar()
        {
            string db = _db.Text.Trim().ToUpperInvariant();
            if (!System.Text.RegularExpressions.Regex.IsMatch(db, "^[A-Z][A-Z0-9_]*$"))
            {
                Dialogo.Aviso(this, "O DB da máquina precisa começar com letra e ter só letras, números e _.");
                return;
            }
            if (_dbEmUso(db))
            {
                Dialogo.Aviso(this, "Já existe uma máquina ou partida no DB " + db + ". Escolha outro (ex.: PP2).");
                return;
            }
            if (_editando)
            {
                Resultado = Opcoes();
                Resultado.Db = db;
                DialogResult = DialogResult.OK;
                return;
            }
            Linhas = Maquinas.CriarPreparador(Opcoes());
            DialogResult = DialogResult.OK;
        }

        ResultadoReceita IRetornaResultadoReceita.Resultado
        {
            get { return new ResultadoReceita { Opcoes = Resultado ?? Opcoes() }; }
        }
    }
}
