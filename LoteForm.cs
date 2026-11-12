using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Genesis
{
    // =======================================================================
    //  Janela "Adicionar em lote": N partidas de uma vez
    // =======================================================================
    internal class LoteForm : Form
    {
        private readonly TextBox _txtSub = new TextBox();
        private readonly TextBox _txtEq = new TextBox();
        private readonly TextBox _txtPref = new TextBox();
        private readonly TextBox _txtDesc = new TextBox();
        private readonly NumericUpDown _numQtd = new NumericUpDown { Minimum = 1, Maximum = 200, Value = 3 };
        private readonly NumericUpDown _numIni = new NumericUpDown { Minimum = 0, Maximum = 999, Value = 1 };
        private readonly ComboBox _cmbTipo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly CheckBox _chkIos = new CheckBox
        {
            Text = "Criar as IOs da partida (endereços depois)",
            AutoSize = true,
            Checked = true
        };

        private readonly CheckBox _chkAuto = new CheckBox { Text = "Operação automática", AutoSize = true, Checked = true };
        private readonly CheckBox _chkInter = new CheckBox { Text = "Intertravamento (contato de condição em aberto)", AutoSize = true, Checked = true };
        private readonly CheckBox _chkAlt = new CheckBox { Text = "Alternância entre pares (1ª↔2ª, 3ª↔4ª...)", AutoSize = true, Checked = true };

        public List<Partida> Partidas = new List<Partida>();
        public List<Io> NovasIos = new List<Io>();

        public LoteForm(string subPadrao, string eqPadrao)
        {
            Text = "Adicionar partidas em lote";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(640, 610);
            Tema.Janela(this);

            _txtSub.Text = subPadrao;
            _txtEq.Text = eqPadrao;
            _txtPref.Text = "BB";
            _txtDesc.Text = "Bomba";
            _cmbTipo.Items.AddRange(Gerador.NomesTiposPartida);
            _cmbTipo.SelectedIndex = 0;
            _cmbTipo.FlatStyle = FlatStyle.Flat;
            foreach (Control c in new Control[] { _txtSub, _txtEq, _txtPref, _txtDesc, _numQtd, _numIni, _cmbTipo })
            {
                c.BackColor = Tema.Campo;
                c.ForeColor = Tema.Texto;
                c.Font = Tema.Normal;
            }
            foreach (NumericUpDown n in new[] { _numQtd, _numIni })
            {
                n.BorderStyle = BorderStyle.FixedSingle;
                n.Font = Tema.Fonte(11f);
            }
            foreach (CheckBox c in new[] { _chkAuto, _chkInter, _chkAlt, _chkIos })
            {
                c.ForeColor = Tema.Texto;
                c.BackColor = Color.Transparent;
                c.Font = Tema.Normal;
                c.Margin = new Padding(0, 0, 0, 8);
            }

            var cab = new FundoDegrade { Dock = DockStyle.Top, Height = 78 };
            Label t = Tema.Titulo("Partidas em lote", 16f);
            t.Location = new Point(20, 10);
            Label st = Tema.Subtitulo("Várias partidas iguais de uma vez: BB1, BB2, BB3…");
            st.Location = new Point(23, 46);
            cab.Controls.Add(t);
            cab.Controls.Add(st);

            var card = new CartaoTabela(2) { Dock = DockStyle.Top };
            card.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 50);
            card.ColumnStyles[1] = new ColumnStyle(SizeType.Percent, 50);
            Linha(card, "Subsistema (ex.: ETE)", _txtSub);
            Linha(card, "Equipamento (ex.: REG)", _txtEq);
            Linha(card, "Prefixo da função (ex.: BB)", _txtPref);
            Linha(card, "Descrição base (ex.: Bomba)", _txtDesc);
            Linha(card, "Quantidade", _numQtd);
            Linha(card, "Numerar a partir de", _numIni);
            Linha(card, "Tipo de partida", _cmbTipo, true);
            var marcas = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Color.Transparent, Margin = new Padding(0, 4, 0, 0) };
            marcas.Controls.AddRange(new Control[] { _chkAuto, _chkInter, _chkAlt, _chkIos });
            card.Controls.Add(marcas);
            card.SetColumnSpan(marcas, 2);

            var corpo = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16, 14, 16, 8), BackColor = Tema.Fundo };
            corpo.Controls.Add(card);

            Button ok = Tema.Botao("Adicionar", true, delegate { Confirmar(); });
            Button cancela = Tema.Botao("Cancelar", false, delegate { DialogResult = DialogResult.Cancel; });
            var botoes = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 62, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 12, 6, 0), BackColor = Tema.FundoEscuro };
            botoes.Controls.Add(ok);
            botoes.Controls.Add(cancela);

            Controls.Add(corpo);
            Controls.Add(botoes);
            Controls.Add(cab);
            AcceptButton = ok;
            CancelButton = cancela;
        }

        private void NovaIo(string nome, string area, string tipo, string descricao)
        {
            NovasIos.Add(new Io { Nome = nome, Area = area, Tipo = tipo, Descricao = descricao });
        }

        /// <summary>
        /// Monta a base dos nomes simbólicos a partir da descrição já numerada do equipamento.
        /// Assim, o número da unidade fica junto ao tipo (BOMBA_1, SOPRADOR_2 etc.),
        /// e não no final do nome formado por SUB_EQ_FN.
        /// </summary>
        private static string NomeBaseIo(string descricao, string fallback)
        {
            string origem = string.IsNullOrWhiteSpace(descricao) ? fallback : descricao;
            string normalizada = (origem ?? "").Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalizada.Length);

            foreach (char c in normalizada)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;

                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToUpperInvariant(c));
                else if (sb.Length > 0 && sb[sb.Length - 1] != '_')
                    sb.Append('_');
            }

            return sb.ToString().Trim('_');
        }

        private static void Linha(CartaoTabela tab, string rotulo, Control campo, bool inteiro = false)
        {
            var b = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, BackColor = Color.Transparent, Margin = new Padding(0, 0, 18, 14) };
            b.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            b.Controls.Add(Tema.Secao(rotulo));
            Control quadro = Tema.Emoldurar(campo);
            quadro.Dock = DockStyle.Fill;
            quadro.Margin = new Padding(0);
            b.Controls.Add(quadro);
            tab.Controls.Add(b);
            if (inteiro) tab.SetColumnSpan(b, 2);
        }

        private void Confirmar()
        {
            var ident = new Regex("^[A-Za-z][A-Za-z0-9_]*$");
            string sub = _txtSub.Text.Trim();
            string eq = _txtEq.Text.Trim();
            string pref = _txtPref.Text.Trim();

            if (!ident.IsMatch(sub) || !ident.IsMatch(eq) || !ident.IsMatch(pref))
            {
                Dialogo.Aviso(this, "Subsistema, equipamento e prefixo da função devem começar com letra e ter só letras, números e _.");
                return;
            }

            int qtd = (int)_numQtd.Value;
            int ini = (int)_numIni.Value;

            if (_chkAlt.Checked && !_chkAuto.Checked)
            {
                Dialogo.Aviso(this, "Alternância exige operação automática.");
                return;
            }
            if (_chkAlt.Checked && qtd % 2 != 0)
            {
                Dialogo.Aviso(this, "Alternância forma pares: use uma quantidade par.");
                return;
            }
            string desc = _txtDesc.Text.Trim();

            for (int k = 0; k < qtd; k++)
            {
                int n = ini + k;
                var p = new Partida
                {
                    Sub = sub,
                    Eq = eq,
                    Fn = pref + n,
                    Tipo = (string)_cmbTipo.SelectedItem,
                    Descricao = Maquinas.NumerarEquipamento(desc, n),
                    Auto = _chkAuto.Checked,
                    Intertravamento = _chkInter.Checked,
                    Alternancia = _chkAlt.Checked
                };

                if (_chkIos.Checked)
                {
                    Gerador.TipoPartida tp = Gerador.BuscarTipo(p.Tipo);
                    string baseNome = NomeBaseIo(p.Descricao, sub + "_" + eq + "_" + p.Fn);
                    string d = p.Descricao;

                    if (tp.Valvula && tp.Analogica)
                    {
                        p.TagRef = "REF_POS_" + baseNome;
                        NovaIo(p.TagRef, "AO", "Int", "Referência de posição " + d);
                        NovaIo(baseNome + "_SET_POS_AUX", "MEM", "Int", "Auxiliar set posição " + d);
                        if (tp.Ai)
                        {
                            p.TagPosAi = "LEI_POS_" + baseNome;
                            NovaIo(p.TagPosAi, "AI", "Int", "Leitura de posição " + d);
                            NovaIo(baseNome + "_LEI_POS_AUX", "MEM", "Int", "Auxiliar leitura posição " + d);
                        }
                    }
                    else if (tp.Valvula)
                    {
                        if (tp.Di2)
                        {
                            p.TagConf = "CONF_ABR_" + baseNome;
                            p.TagConfFec = "CONF_FEC_" + baseNome;
                            NovaIo(p.TagConf, "DI", "Bool", "Confirmação aberta " + d);
                            NovaIo(p.TagConfFec, "DI", "Bool", "Confirmação fechada " + d);
                        }
                        p.TagHab = "ABRE_" + baseNome;
                        NovaIo(p.TagHab, "DO", "Bool", "Abre " + d);
                        if (tp.Do2)
                        {
                            p.TagFec = "FECHA_" + baseNome;
                            NovaIo(p.TagFec, "DO", "Bool", "Fecha " + d);
                        }
                    }
                    else if (tp.Bypass)
                    {
                        if (!tp.Rede)
                        {
                            p.TagHab = "HAB_" + baseNome;
                            NovaIo(p.TagHab, "DO", "Bool", "Habilita drive " + d);
                        }
                        p.TagHabC1 = "HAB_C1_" + baseNome;
                        p.TagHabC2 = "HAB_C2_" + baseNome;
                        NovaIo(p.TagHabC1, "DO", "Bool", "Habilita contator 1 " + d);
                        NovaIo(p.TagHabC2, "DO", "Bool", "Habilita contator 2 " + d);
                    }
                    else if (!tp.Rede)
                    {
                        if (!tp.Drive)
                        {
                            p.TagConf = "CONF_" + baseNome;
                            NovaIo(p.TagConf, "DI", "Bool", "Confirmação " + d);
                        }
                        p.TagHab = "HAB_" + baseNome;
                        NovaIo(p.TagHab, "DO", "Bool", "Habilita " + d);
                    }

                    if (tp.Drive)
                    {
                        p.TagDefDrive = "DEF_" + baseNome;
                        NovaIo(p.TagDefDrive, "DI", "Bool", "Defeito do drive " + d);
                    }
                    if (tp.Velocidade)
                    {
                        p.TagRef = "REF_VEL_" + baseNome;
                        NovaIo(p.TagRef, "AO", "Int", "Referência de velocidade " + d);
                    }

                    // espelho de defeito: TagDef fica vazia e o gerador usa SUB_EQ_FN_LEI_DEF
                    NovaIo(baseNome + "_LEI_DEF", "MEM", "Bool", "Defeito " + d);
                }

                Partidas.Add(p);
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}