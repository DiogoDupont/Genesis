using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Genesis.Hardware
{
    /// <summary>
    /// Form simples para escolher um módulo de IO e a quantidade. Chamado pela PaginaHardware
    /// quando o usuário clica em "Adicionar módulo".
    /// </summary>
    internal class EscolherModuloForm : Form
    {
        public Modulo Modulo { get; private set; }

        private readonly ComboBox _cmbFamilia;
        private readonly ComboBox _cmbModulo;
        private readonly NumericUpDown _numQtd;
        private readonly Label _lblCanais;
        private readonly ProjetoHardware _projeto;

        private readonly string _familiaFixa;

        public EscolherModuloForm(ProjetoHardware projeto) : this(projeto, null) { }

        public EscolherModuloForm(ProjetoHardware projeto, string familiaFixa)
        {
            _projeto = projeto;
            _familiaFixa = familiaFixa;
            Text = "Adicionar módulo de IO";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 340);
            Tema.Janela(this);

            var cab = new FundoDegrade { Dock = DockStyle.Top, Height = 72 };
            var t = Tema.Titulo("Adicionar módulo", 15f);
            t.Location = new Point(18, 8);
            var st = Tema.Subtitulo("SB (dentro do CLP) · SM (rack do CLP) · ET200 (remota)");
            st.Location = new Point(21, 42);
            cab.Controls.Add(t);
            cab.Controls.Add(st);

            var card = new CartaoTabela(2) { Dock = DockStyle.Fill, Margin = new Padding(16, 14, 16, 8) };
            card.ColumnStyles[0].Width = 160;

            // família
            var rotFam = Tema.Secao("Família");
            card.Controls.Add(rotFam, 0, 0);
            _cmbFamilia = Tema.Lista(false);
            _cmbFamilia.Items.AddRange(new object[] { "Signal Board (SB)", "Signal Module (SM)", "Remota ET200" });
            if (_familiaFixa != null)
            {
                // trava a família (chamado de dentro de uma remota: só ET200)
                _cmbFamilia.SelectedItem = _familiaFixa;
                _cmbFamilia.Enabled = false;
            }
            else
            {
                _cmbFamilia.SelectedIndex = 0;
            }
            _cmbFamilia.SelectedIndexChanged += delegate { ReconstruirModulos(); };
            Control qFam = Tema.Emoldurar(_cmbFamilia);
            qFam.Dock = DockStyle.Fill;
            qFam.Margin = new Padding(0, 0, 0, 12);
            card.Controls.Add(qFam, 1, 0);

            // módulo
            var rotMod = Tema.Secao("Módulo");
            card.Controls.Add(rotMod, 0, 1);
            _cmbModulo = Tema.Lista(false);
            _cmbModulo.SelectedIndexChanged += delegate { AtualizarCanais(); };
            Control qMod = Tema.Emoldurar(_cmbModulo);
            qMod.Dock = DockStyle.Fill;
            qMod.Margin = new Padding(0, 0, 0, 12);
            card.Controls.Add(qMod, 1, 1);

            // quantidade
            var rotQtd = Tema.Secao("Quantidade");
            card.Controls.Add(rotQtd, 0, 2);
            _numQtd = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 32,
                Value = 1,
                Width = 80,
                Height = 28,
                BackColor = Tema.Campo,
                ForeColor = Tema.Texto,
                Font = Tema.Normal,
                BorderStyle = BorderStyle.FixedSingle
            };
            _numQtd.ValueChanged += delegate { AtualizarCanais(); };
            Control qQtd = Tema.Emoldurar(_numQtd);
            qQtd.Dock = DockStyle.Left;
            qQtd.Width = 120;
            qQtd.Margin = new Padding(0, 0, 0, 12);
            card.Controls.Add(qQtd, 1, 2);

            // resumo
            var rotRes = Tema.Secao("Canais que serão adicionados");
            card.Controls.Add(rotRes, 0, 3);
            _lblCanais = new Label
            {
                AutoSize = true,
                Font = Tema.Pequena,
                ForeColor = Tema.TextoSec,
                BackColor = Color.Transparent,
                Margin = new Padding(0, 6, 0, 4)
            };
            card.Controls.Add(_lblCanais, 1, 3);

            var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0), BackColor = Tema.Fundo };
            corpo.Controls.Add(card);

            var botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 12, 6, 0), BackColor = Tema.FundoEscuro };
            var ok = Tema.Botao("Adicionar", true, delegate { Confirmar(); });
            var cancela = Tema.Botao("Cancelar", false, delegate { DialogResult = DialogResult.Cancel; });
            botoes.Controls.Add(ok);
            botoes.Controls.Add(cancela);

            Controls.Add(corpo);
            Controls.Add(botoes);
            Controls.Add(cab);
            AcceptButton = ok;
            CancelButton = cancela;

            ReconstruirModulos();
        }

        private void ReconstruirModulos()
        {
            _cmbModulo.Items.Clear();
            IEnumerable<ModuloInfo> fonte;
            int idx = _familiaFixa == "Remota ET200" ? 2 : _cmbFamilia.SelectedIndex;
            switch (idx)
            {
                case 1: fonte = CatalogoModulos.G2.Where(m => m.Familia == "SM"); break;
                case 2: fonte = CatalogoModulos.Et200; break;
                default: fonte = CatalogoModulos.G2.Where(m => m.Familia == "SB"); break;
            }
            foreach (ModuloInfo m in fonte) _cmbModulo.Items.Add(m);
            if (_cmbModulo.Items.Count > 0) _cmbModulo.SelectedIndex = 0;
            AtualizarCanais();
        }

        private void AtualizarCanais()
        {
            ModuloInfo m = _cmbModulo.SelectedItem as ModuloInfo;
            if (m == null) { _lblCanais.Text = ""; return; }
            int q = (int)_numQtd.Value;
            _lblCanais.Text = m.Nome + " × " + q + "  =  "
                + (m.Di * q) + " DI · " + (m.Dq * q) + " DQ · " + (m.Ai * q) + " AI · " + (m.Aq * q) + " AQ";
        }

        private void Confirmar()
        {
            ModuloInfo m = _cmbModulo.SelectedItem as ModuloInfo;
            if (m == null)
            {
                Dialogo.Aviso(this, "Escolha um módulo.");
                return;
            }
            Modulo = new Modulo
            {
                Referencia = m.Referencia,
                Di = m.Di,
                Dq = m.Dq,
                Ai = m.Ai,
                Aq = m.Aq,
                Quantidade = (int)_numQtd.Value
            };
            DialogResult = DialogResult.OK;
        }
    }
}