using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Genesis.Hardware
{
    /// <summary>
    /// Form de adicionar/editar um inversor. Chamado pela PaginaHardware.
    /// </summary>
    internal class InversorForm : Form
    {
        public Inversor Inversor { get; private set; }

        private readonly TextBox _txtTag;
        private readonly ComboBox _cmbModelo;
        private readonly Label _lblGsdml;
        private readonly TextBox _txtTelegrama;
        private readonly TextBox _txtIp;
        private readonly NumericUpDown _numPosicao;
        private readonly ProjetoHardware _projeto;

        public InversorForm(ProjetoHardware projeto, Inversor editar = null)
        {
            _projeto = projeto;
            Text = editar == null ? "Adicionar inversor" : "Editar inversor";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(600, 460);
            Tema.Janela(this);

            var cab = new FundoDegrade { Dock = DockStyle.Top, Height = 72 };
            var t = Tema.Titulo(editar == null ? "Adicionar inversor" : "Editar inversor", 15f);
            t.Location = new Point(18, 8);
            var st = Tema.Subtitulo("G120 · G120C · G120X — telegrama 352 (PZD-6/6)");
            st.Location = new Point(21, 42);
            cab.Controls.Add(t);
            cab.Controls.Add(st);

            var card = new CartaoTabela(2) { Dock = DockStyle.Fill, Margin = new Padding(16, 14, 16, 8) };
            card.ColumnStyles[0].Width = 160;

            int linha = 0;
            Action<string, Control, string> campo = delegate (string rot, Control c, string ajuda)
            {
                card.Controls.Add(Tema.Secao(rot), 0, linha);
                Control q = Tema.Emoldurar(c);
                q.Dock = DockStyle.Fill;
                q.Margin = new Padding(0, 0, 0, 12);
                card.Controls.Add(q, 1, linha);
                linha++;
                if (ajuda != null)
                {
                    var a = Tema.Ajuda(ajuda, 380);
                    a.ForeColor = Tema.TextoFraco;
                    a.Margin = new Padding(0, 0, 0, 12);
                    card.Controls.Add(a, 1, linha);
                    linha++;
                }
            };

            _txtTag = Tema.CaixaTexto();
            if (editar != null) _txtTag.Text = editar.Tag;
            campo("Nome PROFINET", _txtTag, "Ex.: inv_fl1_mic1. Usado como nome do dispositivo na topologia PROFINET.");

            _cmbModelo = Tema.Lista(false);
            foreach (InversorInfo i in CatalogoInversores.Todos) _cmbModelo.Items.Add(i);
            _cmbModelo.SelectedIndexChanged += delegate { AoMudarModelo(); };
            campo("Modelo", _cmbModelo, "Define o GSDML associado (já vem preenchido).");

            _lblGsdml = new Label
            {
                AutoSize = false,
                Height = Moldura.AlturaPadrao,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Tema.Mono,
                ForeColor = Tema.TextoSec,
                BackColor = Color.Transparent,
                Padding = new Padding(4, 0, 0, 0)
            };
            campo("GSDML", _lblGsdml, "Arquivo GSDML que precisa estar instalado no TIA Portal.");

            _txtTelegrama = Tema.CaixaTexto();
            _txtTelegrama.Text = "352";
            campo("Telegrama", _txtTelegrama, "352 = PZD-6/6 (padrão).");

            _txtIp = Tema.CaixaTexto();
            if (editar != null) _txtIp.Text = editar.IpAddress;
            campo("Endereço IP", _txtIp, "Ex.: 192.168.0.10. Obrigatório se o projeto é via PROFINET.");

            _numPosicao = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 255,
                Value = editar != null ? editar.PosicaoTopologia : ProximaPosicao(),
                Width = 80,
                Height = 28,
                BackColor = Tema.Campo,
                ForeColor = Tema.Texto,
                Font = Tema.Normal,
                BorderStyle = BorderStyle.FixedSingle
            };
            Control qPos = Tema.Emoldurar(_numPosicao);
            qPos.Dock = DockStyle.Left;
            qPos.Width = 120;
            campo("Posição na topologia", qPos, "0 = não definida. A ordem física no cabo PROFINET.");

            var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0), BackColor = Tema.Fundo };
            corpo.Controls.Add(card);

            var botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 12, 6, 0), BackColor = Tema.FundoEscuro };
            var ok = Tema.Botao(editar == null ? "Adicionar" : "Salvar", true, delegate { Confirmar(); });
            var cancela = Tema.Botao("Cancelar", false, delegate { DialogResult = DialogResult.Cancel; });
            botoes.Controls.Add(ok);
            botoes.Controls.Add(cancela);

            Controls.Add(corpo);
            Controls.Add(botoes);
            Controls.Add(cab);
            AcceptButton = ok;
            CancelButton = cancela;

            if (editar != null)
            {
                var info = CatalogoInversores.PorModelo(editar.Modelo);
                if (info != null) _cmbModelo.SelectedItem = info;
                _txtTelegrama.Text = editar.Telegrama;
                _lblGsdml.Text = editar.Gsdml;
            }
            else if (_cmbModelo.Items.Count > 0) _cmbModelo.SelectedIndex = 0;
        }

        private int ProximaPosicao()
        {
            return _projeto.Inversores.Count == 0 ? 1 : _projeto.Inversores.Max(i => i.PosicaoTopologia) + 1;
        }

        private void AoMudarModelo()
        {
            var info = _cmbModelo.SelectedItem as InversorInfo;
            if (info == null) { _lblGsdml.Text = ""; return; }
            _lblGsdml.Text = info.Gsdml;
            _txtTelegrama.Text = info.TelegramaPadrao;
        }

        private void Confirmar()
        {
            string tag = (_txtTag.Text ?? "").Trim();
            if (tag.Length == 0) { Dialogo.Aviso(this, "Informe o nome PROFINET."); return; }
            var info = _cmbModelo.SelectedItem as InversorInfo;
            if (info == null) { Dialogo.Aviso(this, "Escolha o modelo."); return; }

            string ip = (_txtIp.Text ?? "").Trim();
            if (ip.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(ip, @"^\d{1,3}(\.\d{1,3}){3}$"))
            {
                Dialogo.Aviso(this, "Endereço IP inválido. Use o formato 192.168.0.10.");
                return;
            }

            Inversor = new Inversor
            {
                Tag = tag,
                Modelo = info.Modelo,
                Gsdml = info.Gsdml,
                Telegrama = (_txtTelegrama.Text ?? "352").Trim(),
                IpAddress = ip,
                PosicaoTopologia = (int)_numPosicao.Value,
                NomeProfinet = tag
            };
            DialogResult = DialogResult.OK;
        }
    }
}