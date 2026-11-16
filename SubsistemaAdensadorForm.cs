using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Genesis.Receitas;

namespace Genesis
{
    internal class SubsistemaAdensadorForm : Form, IRetornaResultadoReceita
    {
        public OpcoesSubsistemaAdensador Opcoes { get; private set; }
        public string ReceitaId { get; set; } = "SubsistemaAdensador";
        ResultadoReceita IRetornaResultadoReceita.Resultado { get { return new ResultadoReceita { Opcoes = Opcoes }; } }

        private readonly TextBox _db, _desc;
        private readonly ComboBox _cmbMot, _cmbTipoAli, _cmbTipoHig, _cmbTipoVhi, _cmbTipoNivAli, _cmbLigHig, _cmbDesHig, _cmbTipoNivLad;
        private readonly CheckBox _chkAli, _chkNivAli, _chkHig, _chkVhi, _chkIntermitenteHig, _chkNivLad;
        private readonly TextBox _txtTagNivAli, _txtTagNivLad;

        private readonly CheckBox _chkAgitLad, _chkAgitEqu, _chkFloc, _chkBombaLad, _chkBombaFlc, _chkAgitFlc, _chkNivFlc;
        private readonly ComboBox _cmbAgitLad, _cmbAgitEqu, _cmbFloc, _cmbBombaLad, _cmbBombaFlc, _cmbAgitFlc;

        private readonly Func<string, bool> _dbEmUso;
        private readonly bool _editando;

        public SubsistemaAdensadorForm(string sugestaoDb, Func<string, bool> dbEmUso, OpcoesSubsistemaAdensador editar = null)
        {
            _dbEmUso = dbEmUso;
            _editando = editar != null;
            Opcoes = editar ?? new OpcoesSubsistemaAdensador { Db = sugestaoDb };

            Text = _editando ? "Reconfigurar Subsistema: Adensador" : "Novo Subsistema: Adensador";
            Size = new Size(920, 880);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false; MinimizeBox = false;
            Tema.Janela(this);

            var cab = new FundoDegrade { Dock = DockStyle.Top, Height = 78 };
            var t = Tema.Titulo(Maquinas.SubsistemaAdensador, 16f); t.Location = new Point(20, 10);
            var st = Tema.Subtitulo("Máquina Adensador + periféricos opcionais"); st.Location = new Point(23, 46);
            cab.Controls.Add(t); cab.Controls.Add(st);

            var corpo = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(16, 14, 16, 8), BackColor = Tema.Fundo };
            corpo.Resize += delegate {
                int w = corpo.ClientSize.Width - corpo.Padding.Horizontal - 20;
                foreach (Control c in corpo.Controls) c.Width = w;
            };

            string[] tipos = Gerador.NomesTiposPartida.Where(x => !x.StartsWith("Válvula")).ToArray();
            string[] tiposValv = Gerador.NomesTiposPartida.Where(x => x.StartsWith("Válvula")).ToArray();
            string[] tiposNiv = { "Sensor nível baixo digital", "Sensor nível analógico" };
            string[] tempos = { "Segundos", "Minutos", "Horas" };

            var cardGeral = new CartaoTabela(2) { AutoSize = true, Margin = new Padding(0, 0, 0, 12) };
            cardGeral.ColumnStyles[0].Width = 180;
            _db = Tema.CaixaTexto(); _db.Text = Opcoes.Db;
            _desc = Tema.CaixaTexto(); _desc.Text = Opcoes.Descricao;
            cardGeral.Controls.Add(CriarLabel("DB do Subsistema"), 0, 0);
            cardGeral.Controls.Add(EmoldurarDock(_db), 1, 0);
            cardGeral.Controls.Add(CriarLabel("Descrição"), 0, 1);
            cardGeral.Controls.Add(EmoldurarDock(_desc), 1, 1);
            corpo.Controls.Add(cardGeral);

            var cardAcion = new CartaoTabela(2) { AutoSize = true, Margin = new Padding(0, 0, 0, 12) };
            cardAcion.ColumnStyles[0].Width = 180;
            _cmbMot = CriarCombo(tipos, Opcoes.TipoMotor);
            cardAcion.Controls.Add(CriarLabel("Motor do Adensador"), 0, 0);
            cardAcion.Controls.Add(EmoldurarDock(_cmbMot), 1, 0);
            corpo.Controls.Add(cardAcion);

            var cardAli = new CartaoTabela(2) { AutoSize = true, Margin = new Padding(0, 0, 0, 12) };
            cardAli.ColumnStyles[0].Width = 180;
            _chkAli = Tema.Marcador("Incluir Bomba de Alimentação"); _chkAli.Checked = Opcoes.TemBombaAlim;
            _cmbTipoAli = CriarCombo(tipos, Opcoes.TipoBombaAlim);
            cardAli.Controls.Add(CriarLabel("Bomba Alimentação"), 0, 0);
            cardAli.Controls.Add(CriarLinhaBomba(_chkAli, _cmbTipoAli), 1, 0);

            _chkNivAli = Tema.Marcador("Incluir Nível"); _chkNivAli.Checked = Opcoes.TemNivelAlim;
            _cmbTipoNivAli = CriarCombo(tiposNiv, Opcoes.TipoNivelAlim);
            _txtTagNivAli = Tema.CaixaTexto(); _txtTagNivAli.Text = "";
            cardAli.Controls.Add(CriarLabel("Nível Alimentação"), 0, 1);
            cardAli.Controls.Add(CriarLinhaNivel(_chkNivAli, _cmbTipoNivAli, _txtTagNivAli), 1, 1);
            corpo.Controls.Add(cardAli);

            var cardHig = new CartaoTabela(2) { AutoSize = true, Margin = new Padding(0, 0, 0, 12) };
            cardHig.ColumnStyles[0].Width = 180;
            _chkHig = Tema.Marcador("Incluir Bomba Higienização"); _chkHig.Checked = Opcoes.TemBombaHig;
            _cmbTipoHig = CriarCombo(tipos, Opcoes.TipoBombaHig);
            cardHig.Controls.Add(CriarLabel("Bomba Higienização"), 0, 0);
            cardHig.Controls.Add(CriarLinhaBomba(_chkHig, _cmbTipoHig), 1, 0);

            _chkVhi = Tema.Marcador("Incluir Válvula Higienização"); _chkVhi.Checked = Opcoes.TemValvulaHig;
            _cmbTipoVhi = CriarCombo(tiposValv, Opcoes.TipoValvulaHig);
            cardHig.Controls.Add(CriarLabel("Válvula Higienização"), 0, 1);
            cardHig.Controls.Add(CriarLinhaBomba(_chkVhi, _cmbTipoVhi), 1, 1);

            _chkIntermitenteHig = Tema.Marcador("Ativar Modo Intermitente"); _chkIntermitenteHig.Checked = Opcoes.TemIntermitenteHig;
            _cmbLigHig = CriarCombo(tempos, Opcoes.TempoLigadoHig);
            _cmbDesHig = CriarCombo(tempos, Opcoes.TempoDesligadoHig);
            cardHig.Controls.Add(CriarLabel("Modo Intermitente"), 0, 2);
            cardHig.Controls.Add(CriarLinhaIntermitente(_chkIntermitenteHig, _cmbLigHig, _cmbDesHig), 1, 2);
            corpo.Controls.Add(cardHig);

            var cardPer = new CartaoTabela(2) { AutoSize = true, Margin = new Padding(0, 0, 0, 12) };
            cardPer.ColumnStyles[0].Width = 180;

            Action<string, CheckBox, ComboBox> addPer = delegate (string label, CheckBox chk, ComboBox cmb)
            {
                int l = cardPer.RowCount - 1;
                cardPer.Controls.Add(CriarLabel(label), 0, l);
                cardPer.Controls.Add(CriarLinhaBomba(chk, cmb), 1, l);
            };

            _chkAgitLad = Tema.Marcador("Incluir Agitador Lodo"); _chkAgitLad.Checked = Opcoes.IncluirAgitLodoAdensado;
            _cmbAgitLad = CriarCombo(tipos, Opcoes.TipoAgitLodo);
            addPer("Agitador Lodo Adensado", _chkAgitLad, _cmbAgitLad);

            _chkNivLad = Tema.Marcador("Incluir Nível"); _chkNivLad.Checked = Opcoes.TemNivelLad;
            _cmbTipoNivLad = CriarCombo(tiposNiv, Opcoes.TipoNivelLad);
            _txtTagNivLad = Tema.CaixaTexto(); _txtTagNivLad.Text = "";
            int lNiv = cardPer.RowCount - 1;
            cardPer.Controls.Add(CriarLabel("Nível Lodo"), 0, lNiv);
            cardPer.Controls.Add(CriarLinhaNivel(_chkNivLad, _cmbTipoNivLad, _txtTagNivLad), 1, lNiv);

            _chkAgitEqu = Tema.Marcador("Incluir Agitador Equalização"); _chkAgitEqu.Checked = Opcoes.IncluirAgitTanqueEqualizacao;
            _cmbAgitEqu = CriarCombo(tipos, Opcoes.TipoAgitEqual);
            addPer("Agitador Tanque Equalização", _chkAgitEqu, _cmbAgitEqu);

            _chkFloc = Tema.Marcador("Incluir Floculador"); _chkFloc.Checked = Opcoes.IncluirFloculador;
            _cmbFloc = CriarCombo(tipos, Opcoes.TipoFloculador);
            addPer("Floculador Mecânico", _chkFloc, _cmbFloc);

            _chkBombaLad = Tema.Marcador("Incluir Bomba Lodo"); _chkBombaLad.Checked = Opcoes.IncluirBombaLodoAdensado;
            _cmbBombaLad = CriarCombo(tipos, Opcoes.TipoBombaLodo);
            addPer("Bomba Lodo Adensado", _chkBombaLad, _cmbBombaLad);

            _chkBombaFlc = Tema.Marcador("Incluir Bomba Floculante"); _chkBombaFlc.Checked = Opcoes.IncluirBombaFloculante;
            _cmbBombaFlc = CriarCombo(tipos, Opcoes.TipoBombaFloc);
            addPer("Bomba Floculante", _chkBombaFlc, _cmbBombaFlc);

            _chkNivFlc = Tema.Marcador("Incluir Nível Baixo Tanque Floculante"); _chkNivFlc.Checked = Opcoes.TemNivelFlc;
            int lNivFlc = cardPer.RowCount - 1;
            cardPer.Controls.Add(CriarLabel("Nível Floculante"), 0, lNivFlc);
            cardPer.Controls.Add(CriarLinhaBomba(_chkNivFlc, new ComboBox { Visible = false }), 1, lNivFlc);

            _chkAgitFlc = Tema.Marcador("Incluir Agitador Floculante"); _chkAgitFlc.Checked = Opcoes.IncluirAgitFloculante;
            _cmbAgitFlc = CriarCombo(tipos, Opcoes.TipoAgitFloc);
            addPer("Agitador Floculante", _chkAgitFlc, _cmbAgitFlc);

            corpo.Controls.Add(cardPer);

            var botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 12, 6, 0), BackColor = Tema.FundoEscuro };
            var ok = Tema.Botao(_editando ? "Aplicar" : "Criar", true, delegate { Confirmar(); });
            var cancela = Tema.Botao("Cancelar", false, delegate { DialogResult = DialogResult.Cancel; });
            var btnPerfil = Tema.Botao("Salvar como perfil…", false, delegate { SalvarComoPerfil(); }, "salvar");
            botoes.Controls.Add(ok); botoes.Controls.Add(cancela); botoes.Controls.Add(btnPerfil);

            Controls.Add(corpo); Controls.Add(botoes); Controls.Add(cab);
            AcceptButton = ok; CancelButton = cancela;
        }

        private Label CriarLabel(string txt) { return new Label { Text = txt, Font = Tema.Negrito, ForeColor = Tema.Texto, BackColor = Color.Transparent, AutoSize = true, Margin = new Padding(0, 10, 10, 10) }; }
        private Control EmoldurarDock(Control c) { var q = Tema.Emoldurar(c); q.Dock = DockStyle.Fill; q.Margin = new Padding(0, 4, 0, 4); return q; }
        
        private ComboBox CriarCombo(string[] itens, string selecionado) 
        { 
            var c = Tema.Lista(false); 
            c.Items.AddRange(itens); 
            c.SelectedItem = selecionado; 
            if (c.SelectedIndex < 0 && c.Items.Count > 0) c.SelectedIndex = 0; 
            c.DropDownWidth = 350; 
            c.MinimumSize = new Size(280, 28); 
            return c; 
        }

        private Control CriarLinhaBomba(CheckBox chk, ComboBox cmb)
        {
            var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 4, 0, 4) };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            chk.Margin = new Padding(0, 8, 10, 0); p.Controls.Add(chk, 0, 0);
            p.Controls.Add(EmoldurarDock(cmb), 1, 0);
            chk.CheckedChanged += delegate { cmb.Enabled = chk.Checked; };
            cmb.Enabled = chk.Checked;
            if (!cmb.Visible) { p.Controls.Remove(cmb); }
            return p;
        }

        private Control CriarLinhaNivel(CheckBox chk, ComboBox cmb, TextBox txtTag)
        {
            var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 4, 0, 4) };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));

            chk.Margin = new Padding(0, 8, 10, 0); p.Controls.Add(chk, 0, 0);
            p.Controls.Add(EmoldurarDock(cmb), 1, 0);
            p.Controls.Add(EmoldurarDock(txtTag), 2, 0);

            chk.CheckedChanged += delegate { cmb.Enabled = chk.Checked; txtTag.Enabled = chk.Checked; };
            cmb.Enabled = chk.Checked; txtTag.Enabled = chk.Checked;
            return p;
        }

        private Control CriarLinhaIntermitente(CheckBox chk, ComboBox cmbLig, ComboBox cmbDes)
        {
            var p = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true, BackColor = Color.Transparent, Margin = new Padding(0, 4, 0, 4) };
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            chk.Margin = new Padding(0, 8, 10, 0); p.Controls.Add(chk, 0, 0);
            var q1 = EmoldurarDock(cmbLig); q1.Margin = new Padding(0, 0, 10, 0); p.Controls.Add(q1, 1, 0);
            p.Controls.Add(EmoldurarDock(cmbDes), 2, 0);

            chk.CheckedChanged += delegate { cmbLig.Enabled = chk.Checked; cmbDes.Enabled = chk.Checked; };
            cmbLig.Enabled = chk.Checked; cmbDes.Enabled = chk.Checked;
            return p;
        }

        private void LerTela()
        {
            Opcoes.Db = _db.Text.Trim().ToUpperInvariant(); 
            Opcoes.Descricao = _desc.Text.Trim();
            Opcoes.TipoMotor = Convert.ToString(_cmbMot.SelectedItem);
            Opcoes.TemBombaAlim = _chkAli.Checked; Opcoes.TipoBombaAlim = Convert.ToString(_cmbTipoAli.SelectedItem);
            Opcoes.TemNivelAlim = _chkNivAli.Checked; Opcoes.TipoNivelAlim = Convert.ToString(_cmbTipoNivAli.SelectedItem);
            Opcoes.TemBombaHig = _chkHig.Checked; Opcoes.TipoBombaHig = Convert.ToString(_cmbTipoHig.SelectedItem);
            Opcoes.TemValvulaHig = _chkVhi.Checked; Opcoes.TipoValvulaHig = Convert.ToString(_cmbTipoVhi.SelectedItem);
            Opcoes.TemIntermitenteHig = _chkIntermitenteHig.Checked; Opcoes.TempoLigadoHig = Convert.ToString(_cmbLigHig.SelectedItem); Opcoes.TempoDesligadoHig = Convert.ToString(_cmbDesHig.SelectedItem);

            Opcoes.IncluirAgitLodoAdensado = _chkAgitLad.Checked; Opcoes.TipoAgitLodo = Convert.ToString(_cmbAgitLad.SelectedItem);
            Opcoes.TemNivelLad = _chkNivLad.Checked; Opcoes.TipoNivelLad = Convert.ToString(_cmbTipoNivLad.SelectedItem);
            Opcoes.IncluirAgitTanqueEqualizacao = _chkAgitEqu.Checked; Opcoes.TipoAgitEqual = Convert.ToString(_cmbAgitEqu.SelectedItem);
            Opcoes.IncluirFloculador = _chkFloc.Checked; Opcoes.TipoFloculador = Convert.ToString(_cmbFloc.SelectedItem);
            Opcoes.IncluirBombaLodoAdensado = _chkBombaLad.Checked; Opcoes.TipoBombaLodo = Convert.ToString(_cmbBombaLad.SelectedItem);
            Opcoes.IncluirBombaFloculante = _chkBombaFlc.Checked; Opcoes.TipoBombaFloc = Convert.ToString(_cmbBombaFlc.SelectedItem);
            Opcoes.TemNivelFlc = _chkNivFlc.Checked;
            Opcoes.IncluirAgitFloculante = _chkAgitFlc.Checked; Opcoes.TipoAgitFloc = Convert.ToString(_cmbAgitFlc.SelectedItem);
        }

        private void Confirmar()
        {
            LerTela();
            string db = Opcoes.Db;
            if (!System.Text.RegularExpressions.Regex.IsMatch(db, "^[A-Z][A-Z0-9_]*$")) { Dialogo.Aviso(this, "DB inválido."); return; }
            if (_dbEmUso(db) && !_editando) { Dialogo.Aviso(this, "DB já em uso."); return; }

            DialogResult = DialogResult.OK;
        }

        private void SalvarComoPerfil()
        {
            LerTela();
            using (var dlg = new SalvarPerfilForm("Subsistema Adensador padrão " + _db.Text, _desc.Text))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                var perfil = new PerfilReceita { ReceitaId = ReceitaId, Nome = dlg.NomePerfil, Descricao = dlg.DescricaoPerfil, OpcoesXml = PerfisReceita.Serializar(Opcoes, typeof(OpcoesSubsistemaAdensador)) };
                try { PerfisReceita.Salvar(perfil); Dialogo.Info(this, "Perfil salvo."); }
                catch (Exception ex) { Dialogo.Erro(this, ex.Message); }
            }
        }
    }
}