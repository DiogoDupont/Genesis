using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Genesis.Hardware
{
    /// <summary>
    /// Form de adicionar/editar uma remota ET200 e seus módulos.
    /// </summary>
    internal class RemotaForm : Form
    {
        public RemotaEt200 Remota { get; private set; }

        private readonly TextBox _txtTag;
        private readonly ComboBox _cmbInterface;
        private readonly TextBox _txtIp;
        private readonly NumericUpDown _numPosicao;
        private readonly FlowLayoutPanel _listaModulos;
        private readonly List<Modulo> _modulos = new List<Modulo>();
        private readonly ProjetoHardware _projeto;

        public RemotaForm(ProjetoHardware projeto, RemotaEt200 editar = null)
        {
            _projeto = projeto;
            Text = editar == null ? "Adicionar remota ET200" : "Editar remota ET200";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(700, 620);
            Tema.Janela(this);

            var cab = new FundoDegrade { Dock = DockStyle.Top, Height = 72 };
            var t = Tema.Titulo(editar == null ? "Nova remota ET200" : "Editar remota ET200", 15f);
            t.Location = new Point(18, 8);
            var st = Tema.Subtitulo("IM 155-6 PN ST · módulos de IO dentro da remota");
            st.Location = new Point(21, 42);
            cab.Controls.Add(t);
            cab.Controls.Add(st);

            var host = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Tema.Fundo, Padding = new Padding(16, 14, 16, 8) };
            Win.Escurecer(host);
            var coluna = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Tema.Fundo };

            // card 1: dados da remota
            var card1 = new CartaoTabela(2) { AutoSize = true, Margin = new Padding(0, 0, 0, 12), Dock = DockStyle.Fill };
            card1.ColumnStyles[0].Width = 140;
            int linha = 0;
            Action<string, Control, string> campo = delegate (string rot, Control c, string ajuda)
            {
                card1.Controls.Add(Tema.Secao(rot), 0, linha);
                Control q = Tema.Emoldurar(c);
                q.Dock = DockStyle.Fill;
                q.Margin = new Padding(0, 0, 0, 10);
                card1.Controls.Add(q, 1, linha);
                linha++;
                if (ajuda != null)
                {
                    var a = Tema.Ajuda(ajuda, 450);
                    a.ForeColor = Tema.TextoFraco;
                    a.Margin = new Padding(0, 0, 0, 10);
                    card1.Controls.Add(a, 1, linha);
                    linha++;
                }
            };

            _txtTag = Tema.CaixaTexto();
            if (editar != null) _txtTag.Text = editar.Tag;
            campo("Nome PROFINET", _txtTag, "Ex.: et200_poco.");

            _cmbInterface = Tema.Lista(false);
            foreach (RemotaInfo r in CatalogoRemotas.Todos) _cmbInterface.Items.Add(r);
            campo("Interface", _cmbInterface, null);

            _txtIp = Tema.CaixaTexto();
            if (editar != null) _txtIp.Text = editar.IpAddress;
            campo("Endereço IP", _txtIp, "Ex.: 192.168.0.30.");

            _numPosicao = new NumericUpDown { Minimum = 0, Maximum = 255, Value = editar != null ? editar.PosicaoTopologia : 0, Width = 80, Height = 28, BackColor = Tema.Campo, ForeColor = Tema.Texto, Font = Tema.Normal, BorderStyle = BorderStyle.FixedSingle };
            Control qPos = Tema.Emoldurar(_numPosicao);
            qPos.Dock = DockStyle.Left;
            qPos.Width = 120;
            campo("Posição", qPos, "0 = não definida.");
            coluna.Controls.Add(card1);

            // card 2: módulos
            var card2 = new CartaoTabela { AutoSize = true, Margin = new Padding(0, 0, 0, 12), Dock = DockStyle.Fill };
            card2.ColumnStyles[0].Width = 0;
            var topo2 = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 10) };
            topo2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            topo2.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
            topo2.Controls.Add(Tema.TituloCartao("Módulos da remota"), 0, 0);
            var btnAdd = Tema.Botao("Adicionar", false, delegate { AdicionarModulo(); }, "mais");
            btnAdd.AutoSize = false;
            btnAdd.Size = new Size(140, 34);
            btnAdd.Anchor = AnchorStyles.Right;
            topo2.Controls.Add(btnAdd, 1, 0);
            card2.Controls.Add(topo2, 1, 0);

            _listaModulos = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0) };
            card2.Controls.Add(_listaModulos, 1, 1);
            coluna.Controls.Add(card2);

            host.Controls.Add(coluna);

            var botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 12, 6, 0), BackColor = Tema.FundoEscuro };
            var ok = Tema.Botao(editar == null ? "Adicionar" : "Salvar", true, delegate { Confirmar(); });
            var cancela = Tema.Botao("Cancelar", false, delegate { DialogResult = DialogResult.Cancel; });
            botoes.Controls.Add(ok);
            botoes.Controls.Add(cancela);

            Controls.Add(host);
            Controls.Add(botoes);
            Controls.Add(cab);
            AcceptButton = ok;
            CancelButton = cancela;

            // pré-popula
            if (editar != null)
            {
                var info = CatalogoRemotas.PorReferencia(editar.ReferenciaInterface);
                if (info != null) _cmbInterface.SelectedItem = info;
                foreach (Modulo m in editar.Modulos) _modulos.Add(m);
            }
            else if (_cmbInterface.Items.Count > 0) _cmbInterface.SelectedIndex = 0;
            ReconstruirListaModulos();
        }

        private void AdicionarModulo()
        {
            using (var dlg = new EscolherModuloForm(_projeto, "Remota ET200"))
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                _modulos.Add(dlg.Modulo);
                ReconstruirListaModulos();
            }
        }

        private void RemoverModulo(Modulo m)
        {
            _modulos.Remove(m);
            ReconstruirListaModulos();
        }

        private void ReconstruirListaModulos()
        {
            _listaModulos.SuspendLayout();
            foreach (Control c in _listaModulos.Controls.Cast<Control>().ToList()) c.Dispose();
            _listaModulos.Controls.Clear();
            if (_modulos.Count == 0)
            {
                var vazio = Tema.Ajuda("Nenhum módulo adicionado. Use \"Adicionar\" acima.");
                vazio.ForeColor = Tema.TextoFraco;
                _listaModulos.Controls.Add(vazio);
            }
            else
            {
                foreach (Modulo m in _modulos.ToList())
                {
                    Modulo mm = m;
                    ModuloInfo info = CatalogoModulos.PorReferencia(mm.Referencia);
                    string nome = info != null ? info.Nome : mm.Referencia;
                    var linha = new TableLayoutPanel { AutoSize = true, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0, 0, 0, 4), Width = 600 };
                    linha.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                    linha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
                    linha.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));
                    linha.Controls.Add(new Label { Text = nome + "  ×  " + mm.Quantidade, AutoSize = true, Font = Tema.Pequena, ForeColor = Tema.Texto, BackColor = Color.Transparent, Margin = new Padding(0, 8, 8, 4) }, 0, 0);
                    linha.Controls.Add(new Label { Text = "DI" + (mm.Di * mm.Quantidade) + " DQ" + (mm.Dq * mm.Quantidade) + " AI" + (mm.Ai * mm.Quantidade) + " AQ" + (mm.Aq * mm.Quantidade), AutoSize = true, Font = Tema.Pequena, ForeColor = Tema.TextoSec, BackColor = Color.Transparent, Margin = new Padding(0, 8, 8, 4) }, 1, 0);
                    var btnX = Tema.BotaoIcone("lixeira", "Remover", delegate { RemoverModulo(mm); }, null);
                    btnX.AutoSize = false; btnX.Size = new Size(28, 28); btnX.Margin = new Padding(0, 2, 0, 0);
                    linha.Controls.Add(btnX, 2, 0);
                    _listaModulos.Controls.Add(linha);
                }
            }
            _listaModulos.ResumeLayout(true);
        }

        private void Confirmar()
        {
            string tag = (_txtTag.Text ?? "").Trim();
            if (tag.Length == 0) { Dialogo.Aviso(this, "Informe o nome da remota."); return; }
            var info = _cmbInterface.SelectedItem as RemotaInfo;
            if (info == null) { Dialogo.Aviso(this, "Escolha a interface."); return; }
            string ip = (_txtIp.Text ?? "").Trim();
            if (ip.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(ip, @"^\d{1,3}(\.\d{1,3}){3}$"))
            {
                Dialogo.Aviso(this, "Endereço IP inválido.");
                return;
            }
            Remota = new RemotaEt200
            {
                Tag = tag,
                ReferenciaInterface = info.Referencia,
                IpAddress = ip,
                PosicaoTopologia = (int)_numPosicao.Value,
                Modulos = new List<Modulo>(_modulos)
            };
            DialogResult = DialogResult.OK;
        }
    }
}