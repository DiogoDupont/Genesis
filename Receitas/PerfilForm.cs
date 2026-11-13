using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace Genesis.Receitas
{
    /// <summary>
    /// Form de escolha de perfil (aberto antes de criar uma receita, se houver perfis salvos).
    /// Devolve o perfil escolhido ou "null" = usar padrão de fábrica.
    /// </summary>
    internal class PerfilForm : Form
    {
        public PerfilReceita Escolhido { get; private set; }
        public bool UsarPadraoDeFabrica { get; private set; }

        public PerfilForm(string receitaNome, List<PerfilReceita> perfis)
        {
            Text = "Escolher perfil — " + receitaNome;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(560, 420);
            Tema.Janela(this);

            var cab = new FundoDegrade { Dock = DockStyle.Top, Height = 72 };
            var t = Tema.Titulo("Escolher perfil", 15f); t.Location = new Point(18, 8);
            var st = Tema.Subtitulo(receitaNome + " — " + perfis.Count + " perfil(is) salvo(s)"); st.Location = new Point(21, 42);
            cab.Controls.Add(t); cab.Controls.Add(st);

            var lista = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = Tema.Cartao,
                ForeColor = Tema.Texto,
                Font = Tema.Normal,
                BorderStyle = BorderStyle.None,
                IntegralHeight = false
            };
            foreach (var p in perfis)
                lista.Items.Add(p.Nome + (string.IsNullOrEmpty(p.Descricao) ? "" : " — " + p.Descricao));

            var btnApagar = Tema.Botao("Apagar perfil", false, delegate
            {
                if (lista.SelectedIndex < 0) return;
                var p = perfis[lista.SelectedIndex];
                if (!Dialogo.Confirmar(this, "Apagar o perfil \"" + p.Nome + "\"?")) return;
                PerfisReceita.Apagar(p);
                perfis.RemoveAt(lista.SelectedIndex);
                lista.Items.RemoveAt(lista.SelectedIndex);
            }, "lixeira");
            btnApagar.Anchor = AnchorStyles.Left;

            var rodape = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 100, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 12, 6, 0), BackColor = Tema.FundoEscuro, WrapContents = false };
            var ok = Tema.Botao("Usar este perfil", true, delegate
            {
                if (lista.SelectedIndex < 0) { Dialogo.Aviso(this, "Escolha um perfil ou clique em \"Padrão de fábrica\"."); return; }
                Escolhido = perfis[lista.SelectedIndex];
                DialogResult = DialogResult.OK;
            });
            var padrao = Tema.Botao("Padrão de fábrica", false, delegate
            {
                UsarPadraoDeFabrica = true;
                DialogResult = DialogResult.OK;
            });
            var cancela = Tema.Botao("Cancelar", false, delegate { DialogResult = DialogResult.Cancel; });
            rodape.Controls.Add(ok);
            rodape.Controls.Add(cancela);
            rodape.Controls.Add(padrao);
            rodape.Controls.Add(btnApagar);

            var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 14, 16, 8), BackColor = Tema.Fundo };
            corpo.Controls.Add(lista);
            corpo.Controls.Add(rodape);

            Controls.Add(corpo);
            Controls.Add(cab);
            CancelButton = cancela;

            if (lista.Items.Count > 0) lista.SelectedIndex = 0;
        }
    }

    /// <summary>
    /// Form de salvar um novo perfil (aberto pelo botão "Salvar como perfil" no rodapé dos forms de receita).
    /// </summary>
    internal class SalvarPerfilForm : Form
    {
        public string NomePerfil { get; private set; }
        public string DescricaoPerfil { get; private set; }

        public SalvarPerfilForm(string nomeSugerido, string descricaoSugerida)
        {
            Text = "Salvar como perfil";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 280);
            Tema.Janela(this);

            var cab = new FundoDegrade { Dock = DockStyle.Top, Height = 72 };
            var t = Tema.Titulo("Salvar como perfil", 15f); t.Location = new Point(18, 8);
            var st = Tema.Subtitulo("Fica em %AppData%\\Genesis\\perfis — vale para todos os projetos"); st.Location = new Point(21, 42);
            cab.Controls.Add(t); cab.Controls.Add(st);

            var card = new CartaoTabela(1) { Dock = DockStyle.Fill, Margin = new Padding(16, 14, 16, 8) };
            card.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 100);

            var txtNome = Tema.CaixaTexto();
            txtNome.Text = nomeSugerido ?? "";
            var rotNome = Tema.Secao("Nome do perfil");
            card.Controls.Add(rotNome);
            Control qNome = Tema.Emoldurar(txtNome);
            qNome.Dock = DockStyle.Fill;
            qNome.Margin = new Padding(0, 0, 0, 12);
            card.Controls.Add(qNome);

            var txtDesc = Tema.CaixaTexto();
            txtDesc.Text = descricaoSugerida ?? "";
            var rotDesc = Tema.Secao("Descrição (opcional)");
            card.Controls.Add(rotDesc);
            Control qDesc = Tema.Emoldurar(txtDesc);
            qDesc.Dock = DockStyle.Fill;
            qDesc.Margin = new Padding(0, 0, 0, 12);
            card.Controls.Add(qDesc);

            var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0), BackColor = Tema.Fundo };
            corpo.Controls.Add(card);

            var rodape = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 12, 6, 0), BackColor = Tema.FundoEscuro };
            var ok = Tema.Botao("Salvar", true, delegate
            {
                string n = (txtNome.Text ?? "").Trim();
                if (n.Length == 0) { Dialogo.Aviso(this, "Informe o nome do perfil."); return; }
                NomePerfil = n;
                DescricaoPerfil = (txtDesc.Text ?? "").Trim();
                DialogResult = DialogResult.OK;
            });
            var cancela = Tema.Botao("Cancelar", false, delegate { DialogResult = DialogResult.Cancel; });
            rodape.Controls.Add(ok);
            rodape.Controls.Add(cancela);

            Controls.Add(corpo);
            Controls.Add(rodape);
            Controls.Add(cab);
            AcceptButton = ok;
            CancelButton = cancela;
        }
    }
}