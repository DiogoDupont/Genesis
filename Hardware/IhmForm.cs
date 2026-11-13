using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Genesis.Hardware
{
    /// <summary>
    /// Form de adicionar/editar uma IHM (linha MTP Unified, Basic ou Comfort).
    /// Comunicação hoje é sempre S7 (o campo Protocolo fica fixo).
    /// </summary>
    internal class IhmForm : Form
    {
        public Ihm Ihm { get; private set; }

        private readonly TextBox _txtTag;
        private readonly ComboBox _cmbModelo;
        private readonly Label _lblRef;
        private readonly TextBox _txtIp;
        private readonly ProjetoHardware _projeto;

        public IhmForm(ProjetoHardware projeto, Ihm editar = null)
        {
            _projeto = projeto;
            Text = editar == null ? "Adicionar IHM" : "Editar IHM";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 340);
            Tema.Janela(this);

            var cab = new FundoDegrade { Dock = DockStyle.Top, Height = 72 };
            var t = Tema.Titulo(editar == null ? "Adicionar IHM" : "Editar IHM", 15f);
            t.Location = new Point(18, 8);
            var st = Tema.Subtitulo("MTP Unified Basic · MTP Unified Comfort");
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
                    var a = Tema.Ajuda(ajuda, 340);
                    a.ForeColor = Tema.TextoFraco;
                    a.Margin = new Padding(0, 0, 0, 12);
                    card.Controls.Add(a, 1, linha);
                    linha++;
                }
            };

            _txtTag = Tema.CaixaTexto();
            if (editar != null) _txtTag.Text = editar.Tag;
            campo("Nome", _txtTag, "Ex.: HMI_FL1.");

            _cmbModelo = Tema.Lista(false);
            foreach (IhmInfo i in CatalogoIhms.Todos) _cmbModelo.Items.Add(i);
            _cmbModelo.SelectedIndexChanged += delegate { AoMudarModelo(); };
            campo("Modelo", _cmbModelo, "Linha MTP Unified (Basic ou Comfort).");

            _lblRef = new Label
            {
                AutoSize = false,
                Height = Moldura.AlturaPadrao,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = Tema.Mono,
                ForeColor = Tema.TextoSec,
                BackColor = Color.Transparent,
                Padding = new Padding(4, 0, 0, 0)
            };
            campo("Referência", _lblRef, null);

            _txtIp = Tema.CaixaTexto();
            if (editar != null) _txtIp.Text = editar.IpAddress;
            campo("Endereço IP", _txtIp, "Ex.: 192.168.0.20.");

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
                var info = CatalogoIhms.PorReferencia(editar.Referencia);
                if (info != null) _cmbModelo.SelectedItem = info;
                _lblRef.Text = editar.Referencia;
            }
            else if (_cmbModelo.Items.Count > 0) _cmbModelo.SelectedIndex = 0;
        }

        private void AoMudarModelo()
        {
            var info = _cmbModelo.SelectedItem as IhmInfo;
            _lblRef.Text = info != null ? info.Referencia : "";
        }

        private void Confirmar()
        {
            string tag = (_txtTag.Text ?? "").Trim();
            if (tag.Length == 0) { Dialogo.Aviso(this, "Informe o nome."); return; }
            var info = _cmbModelo.SelectedItem as IhmInfo;
            if (info == null) { Dialogo.Aviso(this, "Escolha o modelo."); return; }
            string ip = (_txtIp.Text ?? "").Trim();
            if (ip.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(ip, @"^\d{1,3}(\.\d{1,3}){3}$"))
            {
                Dialogo.Aviso(this, "Endereço IP inválido.");
                return;
            }
            Ihm = new Ihm
            {
                Tag = tag,
                Referencia = info.Referencia,
                IpAddress = ip,
                Protocolo = "S7"
            };
            DialogResult = DialogResult.OK;
        }
    }
}