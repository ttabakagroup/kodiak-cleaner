import KodiakCore
import SwiftUI

/// Applies the chosen theme for the current light or dark look.
struct Themed<Content: View>: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.colorScheme) private var colorScheme
    @ViewBuilder var content: Content

    var body: some View {
        let theme = model.theme(dark: colorScheme == .dark)
        content
            .environment(\.theme, theme)
            .tint(theme.accent)
            .transaction { if !model.settings.animations { $0.animation = nil } }
    }
}

struct RootView: View {
    @EnvironmentObject var model: AppModel

    var body: some View {
        Themed { MainLayout() }
            .frame(minWidth: 940, minHeight: 620)
            .onAppear { model.startup() }
            .onChange(of: model.settings.mode) { model.applyAppearance() }
    }
}

struct MainLayout: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme

    var body: some View {
        HStack(spacing: 0) {
            Sidebar()
            Rectangle().fill(theme.line).frame(width: 1)
            VStack(spacing: 0) {
                if model.showDonationBanner && !model.plan.isSupporter {
                    DonationBanner().padding(.horizontal, 28).padding(.top, 16)
                }
                content.frame(maxWidth: .infinity, maxHeight: .infinity)
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .background(theme.bg)
        }
        .background(theme.bg)
        .sheet(item: $model.dialog) { request in Themed { DialogView(request: request) }.environmentObject(model) }
        .sheet(item: $model.sheet) { sheet in
            Themed {
                switch sheet {
                case .beta: BetaNoticeView()
                case .whatsNew(let previous): ChangesView(previous: previous).onDisappear { model.whatsNewClosed() }
                case .changelog: ChangesView(previous: nil)
                case .triggersHistory: TriggersHistoryView()
                case .donation: DonationView()
                }
            }
            .environmentObject(model)
        }
    }

    @ViewBuilder private var content: some View {
        switch model.page {
        case .home: HomeView()
        case .cleaner: CleanerView()
        case .triggers: TriggersView()
        case .settings: SettingsView()
        case .about: AboutView()
        }
    }
}

struct Sidebar: View {
    @EnvironmentObject var model: AppModel
    @Environment(\.theme) private var theme

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Button { model.page = .home } label: {
                HStack(spacing: 10) {
                    LogoView(size: 40)
                    VStack(alignment: .leading, spacing: 0) {
                        Text("Kodiak").foregroundStyle(theme.brand1)
                        Text("Cleaner").foregroundStyle(theme.brand2)
                    }
                    .font(theme.display(23))
                }
            }
            .buttonStyle(.plain)
            .focusEffectDisabled()
            .accessibilityLabel("Kodiak Cleaner, Getting Started")
            .padding(.bottom, 14)

            Text("Cleaning").font(theme.body(11, weight: .semibold)).foregroundStyle(theme.muted)
                .padding(.leading, 10).padding(.top, 4)
            item(.cleaner, "Cleaner", "sparkles")
            item(.triggers, "Triggers", "bolt.horizontal.circle")
            Spacer()
            if model.plan.isSupporter && model.settings.showBadge {
                Chip(text: model.plan.badgeText, icon: "heart.fill").padding(.leading, 10).padding(.bottom, 6)
            }
            Rectangle().fill(theme.line).frame(height: 1).padding(.vertical, 6)
            item(.settings, "Settings", "gearshape")
            item(.about, "About", "info.circle")
            Text(AppInfo.versionName).font(theme.body(11)).foregroundStyle(theme.muted).padding(.leading, 10).padding(.top, 6)
        }
        .padding(.horizontal, 14)
        .padding(.top, 20)
        .padding(.bottom, 14)
        .frame(width: 220)
        .frame(maxHeight: .infinity)
        .background(theme.surface)
    }

    private func item(_ page: Page, _ title: String, _ icon: String) -> some View {
        Button { model.page = page } label: {
            Label(title, systemImage: icon).labelStyle(SidebarLabelStyle())
        }
        .kcButton(.nav(selected: model.page == page))
        .accessibilityAddTraits(model.page == page ? .isSelected : [])
    }
}

struct SidebarLabelStyle: LabelStyle {
    func makeBody(configuration: Configuration) -> some View {
        HStack(spacing: 10) {
            configuration.icon.frame(width: 18)
            configuration.title
        }
    }
}
